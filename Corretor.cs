using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace CorrigirNFe
{
    internal sealed class Corretor
    {
        internal static readonly XNamespace NS = "http://www.portalfiscal.inf.br/nfe";
        private static readonly XNamespace DS = "http://www.w3.org/2000/09/xmldsig#";

        /// <summary>Valor de indPres usado quando a tag vem ausente. Ajustavel pela linha de comando.</summary>
        public string IndPresPadrao = "9";

        private Resultado _r;

        // ---------- utilitarios ----------

        private static string Nome(XElement e) { return e.Name.LocalName; }

        private static XElement El(XElement pai, string nome)
        {
            return pai == null ? null : pai.Element(NS + nome);
        }

        private static string Txt(XElement pai, string nome)
        {
            XElement e = El(pai, nome);
            return e == null ? null : (e.Value ?? "").Trim();
        }

        /// <summary>Le um decimal do XML. O XML sempre usa ponto, entao a cultura e fixa.</summary>
        private static bool TryDec(string s, out decimal v)
        {
            v = 0m;
            if (string.IsNullOrEmpty(s)) return false;
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
        }

        private static decimal Dec(XElement pai, string nome)
        {
            decimal v;
            return TryDec(Txt(pai, nome), out v) ? v : 0m;
        }

        private static bool Tem(XElement pai, string nome) { return El(pai, nome) != null; }

        /// <summary>
        /// Arredondamento da NF-e: meio para cima (2 casas). NUNCA usar double aqui --
        /// o bug que este programa corrige nasce exatamente de fazer essa conta em float.
        /// </summary>
        internal static decimal R2(decimal v)
        {
            return Math.Round(v, 2, MidpointRounding.AwayFromZero);
        }

        private static string F2(decimal v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>Grava um valor so se ele mudou, registrando a ocorrencia.</summary>
        private bool Set(XElement pai, string nome, decimal novo, Tipo tipo, string local, string rotulo)
        {
            XElement e = El(pai, nome);
            if (e == null) return false;
            string atual = (e.Value ?? "").Trim();
            string texto = F2(novo);
            if (atual == texto) return false;
            e.Value = texto;
            _r.Add(tipo, local, string.Format("{0}: {1} -> {2}", rotulo, atual, texto), true);
            return true;
        }

        // ---------- carga ----------

        /// <summary>
        /// Le o arquivo tolerando o lixo mais comum: BOM, aviso do navegador antes do XML
        /// ("This XML file does not appear to have any style information"), espacos a frente.
        /// </summary>
        public static XDocument Carregar(string caminho, out string erro)
        {
            erro = null;
            string bruto;
            try { bruto = File.ReadAllText(caminho, Encoding.UTF8); }
            catch (Exception ex) { erro = "nao foi possivel ler o arquivo: " + ex.Message; return null; }

            if (bruto.Length > 0 && bruto[0] == '﻿') bruto = bruto.Substring(1);

            int ini = bruto.IndexOf("<?xml", StringComparison.Ordinal);
            if (ini < 0) ini = bruto.IndexOf("<NFe", StringComparison.Ordinal);
            if (ini < 0) ini = bruto.IndexOf("<nfeProc", StringComparison.Ordinal);
            if (ini < 0) ini = bruto.IndexOf("<enviNFe", StringComparison.Ordinal);
            if (ini < 0) { erro = "o arquivo nao parece ser um XML de NF-e"; return null; }
            if (ini > 0) bruto = bruto.Substring(ini);

            try { return XDocument.Parse(bruto, LoadOptions.None); }
            catch (Exception ex) { erro = "XML mal formado: " + ex.Message; return null; }
        }

        // ---------- processamento ----------

        public Resultado Processar(string caminho, XDocument doc)
        {
            _r = new Resultado();
            _r.Ok = true;
            _r.Arquivo = caminho;

            XElement raiz = doc.Root;

            if (Nome(raiz) == "nfeProc")
            {
                _r.Ok = false;
                _r.Erro = "este XML e um nfeProc (nota JA AUTORIZADA pela SEFAZ). "
                        + "Nota autorizada nao se corrige: o caminho e cancelamento ou carta de correcao. "
                        + "Para corrigir e retransmitir, use o XML gerado antes do envio.";
                return _r;
            }

            XElement nfe = Nome(raiz) == "NFe" ? raiz : raiz.Descendants(NS + "NFe").FirstOrDefault();
            if (nfe == null)
            {
                _r.Ok = false;
                _r.Erro = "nao encontrei o elemento <NFe> dentro do arquivo";
                return _r;
            }

            XElement inf = El(nfe, "infNFe");
            if (inf == null)
            {
                _r.Ok = false;
                _r.Erro = "nao encontrei o elemento <infNFe>";
                return _r;
            }

            XElement ide = El(inf, "ide");
            XElement emit = El(inf, "emit");
            _r.Chave = (inf.Attribute("Id") == null ? "" : inf.Attribute("Id").Value ?? "").Replace("NFe", "");
            _r.NumeroNF = Txt(ide, "nNF");
            _r.Emitente = Txt(emit, "xFant");
            if (string.IsNullOrEmpty(_r.Emitente)) _r.Emitente = Txt(emit, "xNome");
            _r.QtdItens = inf.Elements(NS + "det").Count();

            RemoverAssinatura(nfe);
            RemoverGruposVazios(inf);
            RemoverTagsVazias(inf);
            AjustarIde(ide);
            LimparIE(inf);
            Reordenar(inf);
            decimal[] somas = CorrigirItens(inf);
            CorrigirTotais(inf, somas);
            ChecagensFiscais(inf);

            return _r;
        }

        /// <summary>
        /// Qualquer alteracao no conteudo invalida a assinatura digital. Tirar a
        /// assinatura e obrigatorio -- deixa-la seria pior, porque a SEFAZ rejeitaria
        /// por assinatura invalida e o motivo real ficaria escondido.
        /// </summary>
        private void RemoverAssinatura(XElement nfe)
        {
            XElement sig = nfe.Element(DS + "Signature");
            if (sig == null) return;
            _r.TinhaAssinatura = true;
            sig.Remove();
            _r.Add(Tipo.Estrutura, "NFe",
                "assinatura digital removida: o conteudo mudou, entao a nota PRECISA ser assinada de novo antes de transmitir",
                true);
        }

        private void RemoverGruposVazios(XElement inf)
        {
            foreach (string nome in Layout.GruposOpcionais)
            {
                XElement g = El(inf, nome);
                if (g == null) continue;
                bool tudoVazio = g.Elements().All(c => !c.HasElements && string.IsNullOrWhiteSpace(c.Value));
                if (!tudoVazio || !g.Elements().Any()) continue;
                g.Remove();
                _r.Add(Tipo.Estrutura, nome,
                    "grupo <" + nome + "> removido: todas as tags estavam em branco", true);
            }
        }

        private void RemoverTagsVazias(XElement inf)
        {
            var contagem = new Dictionary<string, int>();
            var naoRemoviveis = new Dictionary<string, int>();

            foreach (XElement e in inf.Descendants().ToList())
            {
                if (e.HasElements) continue;
                string nome = Nome(e);
                string valor = (e.Value ?? "").Trim();

                bool vazia = valor.Length == 0;
                // nItemPed = 0 e invalido: o minimo e 1.
                bool zeroInvalido = nome == "nItemPed" && valor == "0";
                if (!vazia && !zeroInvalido) continue;

                if (vazia && Layout.VazioEhValido.Contains(nome)) continue;

                string pai = e.Parent == null ? "" : Nome(e.Parent);
                bool pode = Layout.RemoverSeVazia.Contains(nome)
                         || Layout.RemoverSeVaziaPorPai.Contains(pai + "/" + nome);

                if (pode)
                {
                    string chave = "<" + nome + ">" + (zeroInvalido ? " com valor 0" : " vazia");
                    contagem[chave] = (contagem.ContainsKey(chave) ? contagem[chave] : 0) + 1;
                    e.Remove();
                }
                else
                {
                    string chave = pai + "/" + nome;
                    naoRemoviveis[chave] = (naoRemoviveis.ContainsKey(chave) ? naoRemoviveis[chave] : 0) + 1;
                }
            }

            foreach (var kv in contagem.OrderBy(k => k.Key))
                _r.Add(Tipo.Estrutura, "itens/cadastro",
                    string.Format("{0} ocorrencia(s) de {1} removida(s)", kv.Value, kv.Key), true);

            foreach (var kv in naoRemoviveis.OrderBy(k => k.Key))
                _r.Add(Tipo.Bloqueio, kv.Key,
                    string.Format("tag obrigatoria veio em branco ({0}x) e nao pode ser simplesmente removida -- "
                                + "falta preencher o cadastro na origem", kv.Value), false);
        }

        private void AjustarIde(XElement ide)
        {
            if (ide == null) return;

            if (!Tem(ide, "indPres"))
            {
                ide.Add(new XElement(NS + "indPres", IndPresPadrao));
                _r.Add(Tipo.Estrutura, "ide",
                    "<indPres> ausente (obrigatoria) -- assumido " + IndPresPadrao
                    + " (" + DescreveIndPres(IndPresPadrao) + ")", true);
            }

            // Com indPres nao presencial, a SEFAZ exige o indicador de intermediador.
            string indPres = Txt(ide, "indPres");
            bool exigeIntermed = indPres == "2" || indPres == "3" || indPres == "4" || indPres == "9";
            if (exigeIntermed && !Tem(ide, "indIntermed"))
            {
                ide.Add(new XElement(NS + "indIntermed", "0"));
                _r.Add(Tipo.Estrutura, "ide",
                    "<indIntermed> ausente -- assumido 0 (operacao sem intermediador), exigida porque indPres = "
                    + indPres, true);
            }

            string natOp = Txt(ide, "natOp");
            if (!string.IsNullOrEmpty(natOp) && ParecesTruncada(natOp))
                _r.Add(Tipo.Fiscal, "ide",
                    "<natOp> parece truncada: \"" + natOp + "\" -- nao impede a transmissao, mas sai assim no DANFE", false);
        }

        private static string DescreveIndPres(string v)
        {
            switch (v)
            {
                case "0": return "nao se aplica";
                case "1": return "presencial";
                case "2": return "internet";
                case "3": return "teleatendimento";
                case "4": return "entrega a domicilio";
                case "5": return "presencial fora do estabelecimento";
                case "9": return "nao presencial, outros";
                default: return "valor nao previsto";
            }
        }

        private static bool ParecesTruncada(string s)
        {
            string t = s.TrimEnd().ToLowerInvariant();
            string[] caudas = { " de", " da", " do", " das", " dos", " e", " ou", " em", " para", " com", " a", " o", "-" };
            foreach (string c in caudas) if (t.EndsWith(c, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>IE tem que ser so digitos (ou ISENTO). O FlexCorp manda com mascara.</summary>
        private void LimparIE(XElement inf)
        {
            foreach (string grupo in new[] { "emit", "dest" })
            {
                XElement g = El(inf, grupo);
                XElement ie = El(g, "IE");
                if (ie == null) continue;
                string v = (ie.Value ?? "").Trim();
                if (v.Length == 0 || v == "ISENTO" || v.All(char.IsDigit)) continue;

                string limpo = new string(v.Where(char.IsDigit).ToArray());
                if (limpo.Length < 2)
                {
                    _r.Add(Tipo.Bloqueio, grupo + "/IE",
                        "IE invalida (\"" + v + "\") e sem digitos suficientes para corrigir", false);
                    continue;
                }
                ie.Value = limpo;
                _r.Add(Tipo.Estrutura, grupo + "/IE",
                    "mascara removida: " + v + " -> " + limpo, true);
            }
        }

        /// <summary>
        /// Poe os filhos de cada grupo conhecido na ordem do schema. Se aparecer uma
        /// tag que nao pertence ao grupo, ela e removida quando esta zerada/vazia
        /// (caso do vBCSTRet/vICMSSTRet no ICMSTot) ou vira bloqueio quando tem valor.
        /// </summary>
        private void Reordenar(XElement inf)
        {
            foreach (XElement e in inf.DescendantsAndSelf().ToList())
            {
                string nome = Nome(e);
                string[] ordem;
                if (!Layout.Ordem.TryGetValue(nome, out ordem)) continue;
                if (!e.HasElements) continue;

                var filhos = e.Elements().ToList();
                var desconhecidos = filhos.Where(c => Array.IndexOf(ordem, Nome(c)) < 0).ToList();

                bool abortar = false;
                foreach (XElement d in desconhecidos)
                {
                    string v = (d.Value ?? "").Trim();
                    bool descartavel = !d.HasElements && (v.Length == 0 || v == "0" || v == "0.00");
                    if (descartavel)
                    {
                        d.Remove();
                        _r.Add(Tipo.Estrutura, nome,
                            "<" + Nome(d) + "> removida: nao existe nesse grupo no layout atual", true);
                    }
                    else
                    {
                        abortar = true;
                        _r.Add(Tipo.Bloqueio, nome,
                            "<" + Nome(d) + "> nao pertence a esse grupo e tem conteudo (\"" + v
                            + "\") -- nao reordenei o grupo para nao perder dado", false);
                    }
                }
                if (abortar) continue;

                filhos = e.Elements().ToList();
                var atual = filhos.Select(Nome).ToList();

                // A ordem desejada tem que respeitar a MULTIPLICIDADE: <det> aparece
                // uma vez por item, e comparar contra uma lista com um <det> so
                // acusaria desordem em toda nota com mais de um item.
                var desejada = new List<string>();
                foreach (string n in ordem)
                    for (int k = atual.Count(x => x == n); k > 0; k--)
                        desejada.Add(n);

                if (atual.SequenceEqual(desejada)) continue;

                var foraDeLugar = new List<string>();
                for (int k = 0; k < atual.Count && k < desejada.Count; k++)
                    if (atual[k] != desejada[k] && !foraDeLugar.Contains(atual[k]))
                        foraDeLugar.Add(atual[k]);

                // Remove todos e re-adiciona na ordem correta, preservando duplicatas.
                var porNome = new Dictionary<string, Queue<XElement>>();
                foreach (XElement c in filhos)
                {
                    string n = Nome(c);
                    if (!porNome.ContainsKey(n)) porNome[n] = new Queue<XElement>();
                    porNome[n].Enqueue(c);
                    c.Remove();
                }
                foreach (string n in ordem)
                    while (porNome.ContainsKey(n) && porNome[n].Count > 0)
                        e.Add(porNome[n].Dequeue());

                _r.Add(Tipo.Estrutura, nome,
                    "ordem dos elementos corrigida conforme o schema ("
                    + string.Join(", ", foraDeLugar.Take(6)) + " fora de posicao)", true);
            }
        }

        // ---------- itens ----------

        /// <summary>Recalcula item a item e devolve as somas para a totalizacao.</summary>
        private decimal[] CorrigirItens(XElement inf)
        {
            decimal somaProd = 0m, somaBCicms = 0m, somaICMS = 0m, somaBCibs = 0m;
            decimal somaIBSUF = 0m, somaIBSMun = 0m, somaCBS = 0m;
            decimal somaFrete = 0m, somaSeg = 0m, somaDesc = 0m, somaOutro = 0m;
            decimal somaIPI = 0m, somaST = 0m, somaBCST = 0m, somaDeson = 0m, somaFCPST = 0m;

            foreach (XElement det in inf.Elements(NS + "det"))
            {
                string item = "item " + (det.Attribute("nItem") == null ? "?" : det.Attribute("nItem").Value);
                XElement prod = El(det, "prod");
                if (prod == null) continue;

                decimal qCom = Dec(prod, "qCom");
                decimal vUnCom = Dec(prod, "vUnCom");
                decimal vProd = Dec(prod, "vProd");

                // A regra da SEFAZ: vProd = arredondar(qCom x vUnCom) em 2 casas.
                decimal esperado = R2(qCom * vUnCom);
                if (vProd != esperado && Tem(prod, "vProd"))
                {
                    Set(prod, "vProd", esperado, Tipo.Calculo, item,
                        string.Format("vProd ({0} x {1})", qCom, vUnCom));
                    vProd = esperado;
                }

                somaProd += vProd;
                somaFrete += Dec(prod, "vFrete");
                somaSeg += Dec(prod, "vSeg");
                somaDesc += Dec(prod, "vDesc");
                somaOutro += Dec(prod, "vOutro");

                // vItem espelha o vProd do proprio item.
                if (Tem(det, "vItem"))
                    Set(det, "vItem", vProd, Tipo.Calculo, item, "vItem (deve espelhar vProd)");

                XElement imposto = El(det, "imposto");
                if (imposto == null) continue;

                // ----- ICMS -----
                XElement icms = El(imposto, "ICMS");
                XElement g = icms == null ? null : icms.Elements().FirstOrDefault();
                if (g != null)
                {
                    if (Tem(g, "vBC") && Tem(g, "pICMS") && Tem(g, "vICMS"))
                    {
                        decimal vBC = Dec(g, "vBC");
                        decimal pICMS = Dec(g, "pICMS");
                        Set(g, "vICMS", R2(vBC * pICMS / 100m), Tipo.Calculo, item,
                            string.Format("vICMS ({0} x {1}%)", F2(vBC), F2(pICMS)));
                        somaBCicms += vBC;
                        somaICMS += Dec(g, "vICMS");
                    }
                    somaST += Dec(g, "vICMSST");
                    somaBCST += Dec(g, "vBCST");
                    somaDeson += Dec(g, "vICMSDeson");
                    somaFCPST += Dec(g, "vFCPST");
                }

                // ----- IPI -----
                XElement ipi = El(imposto, "IPI");
                XElement ipiTrib = El(ipi, "IPITrib");
                if (ipiTrib != null) somaIPI += Dec(ipiTrib, "vIPI");

                // ----- IBS / CBS -----
                XElement ibscbs = El(imposto, "IBSCBS");
                XElement gi = El(ibscbs, "gIBSCBS");
                if (gi != null)
                {
                    string cstIbs = Txt(ibscbs, "CST");
                    decimal baseIbs = Dec(gi, "vBC");

                    // CST 000 = tributacao integral: a base e o proprio valor do item.
                    // Em qualquer outro CST pode haver reducao/exclusao, entao so reporto.
                    if (baseIbs != vProd)
                    {
                        if (cstIbs == "000")
                            Set(gi, "vBC", vProd, Tipo.Calculo, item, "vBC do IBS/CBS (deve espelhar vProd)");
                        else
                            _r.Add(Tipo.Fiscal, item,
                                "base do IBS/CBS (" + F2(baseIbs) + ") difere do vProd (" + F2(vProd)
                                + ") com CST " + cstIbs + " -- pode ser reducao legitima, nao alterei", false);
                        baseIbs = Dec(gi, "vBC");
                    }

                    XElement guf = El(gi, "gIBSUF");
                    if (guf != null && Tem(guf, "pIBSUF") && Tem(guf, "vIBSUF"))
                        Set(guf, "vIBSUF", R2(baseIbs * Dec(guf, "pIBSUF") / 100m), Tipo.Calculo, item,
                            string.Format("vIBSUF ({0} x {1}%)", F2(baseIbs), Txt(guf, "pIBSUF")));

                    XElement gmun = El(gi, "gIBSMun");
                    if (gmun != null && Tem(gmun, "pIBSMun") && Tem(gmun, "vIBSMun"))
                        Set(gmun, "vIBSMun", R2(baseIbs * Dec(gmun, "pIBSMun") / 100m), Tipo.Calculo, item,
                            string.Format("vIBSMun ({0} x {1}%)", F2(baseIbs), Txt(gmun, "pIBSMun")));

                    decimal vUF = guf == null ? 0m : Dec(guf, "vIBSUF");
                    decimal vMun = gmun == null ? 0m : Dec(gmun, "vIBSMun");
                    if (Tem(gi, "vIBS"))
                        Set(gi, "vIBS", vUF + vMun, Tipo.Calculo, item, "vIBS (vIBSUF + vIBSMun)");

                    XElement gcbs = El(gi, "gCBS");
                    if (gcbs != null && Tem(gcbs, "pCBS") && Tem(gcbs, "vCBS"))
                        Set(gcbs, "vCBS", R2(baseIbs * Dec(gcbs, "pCBS") / 100m), Tipo.Calculo, item,
                            string.Format("vCBS ({0} x {1}%)", F2(baseIbs), Txt(gcbs, "pCBS")));

                    somaBCibs += baseIbs;
                    somaIBSUF += vUF;
                    somaIBSMun += vMun;
                    somaCBS += gcbs == null ? 0m : Dec(gcbs, "vCBS");
                }
            }

            return new[]
            {
                somaProd, somaBCicms, somaICMS, somaBCibs, somaIBSUF, somaIBSMun, somaCBS,
                somaFrete, somaSeg, somaDesc, somaOutro, somaIPI, somaST, somaBCST, somaDeson, somaFCPST
            };
        }

        // ---------- totais ----------

        private void CorrigirTotais(XElement inf, decimal[] s)
        {
            decimal somaProd = s[0], somaBCicms = s[1], somaICMS = s[2], somaBCibs = s[3];
            decimal somaIBSUF = s[4], somaIBSMun = s[5], somaCBS = s[6];
            decimal somaFrete = s[7], somaSeg = s[8], somaDesc = s[9], somaOutro = s[10];
            decimal somaIPI = s[11], somaST = s[12], somaBCST = s[13], somaDeson = s[14], somaFCPST = s[15];

            XElement total = El(inf, "total");
            if (total == null) return;

            XElement t = El(total, "ICMSTot");
            if (t != null)
            {
                Set(t, "vBC", somaBCicms, Tipo.Calculo, "total", "vBC");
                Set(t, "vICMS", somaICMS, Tipo.Calculo, "total", "vICMS");
                Set(t, "vBCST", somaBCST, Tipo.Calculo, "total", "vBCST");
                Set(t, "vST", somaST, Tipo.Calculo, "total", "vST");
                Set(t, "vFCPST", somaFCPST, Tipo.Calculo, "total", "vFCPST");
                Set(t, "vICMSDeson", somaDeson, Tipo.Calculo, "total", "vICMSDeson");
                Set(t, "vProd", somaProd, Tipo.Calculo, "total", "vProd");
                Set(t, "vFrete", somaFrete, Tipo.Calculo, "total", "vFrete");
                Set(t, "vSeg", somaSeg, Tipo.Calculo, "total", "vSeg");
                Set(t, "vDesc", somaDesc, Tipo.Calculo, "total", "vDesc");
                Set(t, "vOutro", somaOutro, Tipo.Calculo, "total", "vOutro");
                Set(t, "vIPI", somaIPI, Tipo.Calculo, "total", "vIPI");

                decimal vNF = somaProd - somaDesc - somaDeson
                            + somaST + somaFCPST + somaFrete + somaSeg + somaOutro
                            + somaIPI + Dec(t, "vII") + Dec(t, "vIPIDevol") + Dec(t, "vServ");
                Set(t, "vNF", vNF, Tipo.Calculo, "total", "vNF");
            }

            XElement ib = El(total, "IBSCBSTot");
            decimal vIBS = 0m, vCBS = 0m;
            if (ib != null)
            {
                Set(ib, "vBCIBSCBS", somaBCibs, Tipo.Calculo, "total", "vBCIBSCBS");

                XElement gIBS = El(ib, "gIBS");
                if (gIBS != null)
                {
                    XElement guf = El(gIBS, "gIBSUF");
                    if (guf != null) Set(guf, "vIBSUF", somaIBSUF, Tipo.Calculo, "total", "vIBSUF");
                    XElement gmun = El(gIBS, "gIBSMun");
                    if (gmun != null) Set(gmun, "vIBSMun", somaIBSMun, Tipo.Calculo, "total", "vIBSMun");
                    Set(gIBS, "vIBS", somaIBSUF + somaIBSMun, Tipo.Calculo, "total", "vIBS");
                    vIBS = Dec(gIBS, "vIBS");
                }

                XElement gCBS = El(ib, "gCBS");
                if (gCBS != null)
                {
                    Set(gCBS, "vCBS", somaCBS, Tipo.Calculo, "total", "vCBS");
                    vCBS = Dec(gCBS, "vCBS");
                }
            }

            // vNFTot = valor da nota considerando os tributos por fora (IBS, CBS e IS).
            // Nao e a soma dos itens -- esse e o erro que o FlexCorp comete aqui.
            if (Tem(total, "vNFTot"))
            {
                decimal vNF = t == null ? somaProd : Dec(t, "vNF");
                decimal vIS = 0m;
                XElement isTot = El(total, "ISTot");
                if (isTot != null) vIS = Dec(isTot, "vIS");
                Set(total, "vNFTot", vNF + vIBS + vCBS + vIS, Tipo.Calculo, "total",
                    "vNFTot (vNF + IBS + CBS + IS)");
            }
        }

        // ---------- checagens que NAO alteram nada ----------

        private void ChecagensFiscais(XElement inf)
        {
            XElement ide = El(inf, "ide");
            XElement dest = El(inf, "dest");

            var cstZerado = new List<string>();
            var stIncoerente = new List<string>();

            foreach (XElement det in inf.Elements(NS + "det"))
            {
                string item = det.Attribute("nItem") == null ? "?" : det.Attribute("nItem").Value;
                XElement prod = El(det, "prod");
                XElement icms = El(El(det, "imposto"), "ICMS");
                XElement g = icms == null ? null : icms.Elements().FirstOrDefault();
                if (g == null || prod == null) continue;

                string cst = Txt(g, "CST");
                string cfop = Txt(prod, "CFOP");

                // CST 00 = tributada integralmente. Aliquota zero contradiz o proprio CST.
                if (cst == "00" && Tem(g, "pICMS") && Dec(g, "pICMS") == 0m)
                    cstZerado.Add(item);

                // CFOP de substituicao tributaria com grupo de ICMS proprio tributado.
                if (!string.IsNullOrEmpty(cfop) && cfop.Length == 4 && EhCfopST(cfop)
                    && (Nome(g) == "ICMS00" || Nome(g) == "ICMS20"))
                    stIncoerente.Add(item + " (CFOP " + cfop + " / " + Nome(g) + ")");
            }

            if (cstZerado.Count > 0)
                _r.Add(Tipo.Fiscal, "itens " + string.Join(", ", cstZerado),
                    "CST 00 (tributada integralmente) com pICMS 0,00 -- contradicao. "
                    + "Revisar o enquadramento: nao alterei porque mudar CST muda a tributacao", false);

            if (stIncoerente.Count > 0)
                _r.Add(Tipo.Fiscal, "itens " + string.Join(", ", stIncoerente),
                    "CFOP de substituicao tributaria com grupo de ICMS proprio tributado -- "
                    + "normalmente seria CST 60 / grupo ICMS60. Nao alterei", false);

            // Transferencia para filial contribuinte raramente e consumidor final.
            if (Txt(ide, "indFinal") == "1" && Txt(dest, "indIEDest") == "1")
                _r.Add(Tipo.Fiscal, "ide",
                    "indFinal = 1 (consumidor final) com destinatario contribuinte do ICMS "
                    + "(indIEDest = 1) -- em transferencia para revenda o usual e 0. Nao alterei", false);
        }

        private static bool EhCfopST(string cfop)
        {
            string fim = cfop.Substring(1);
            string[] st = { "403", "404", "405", "409", "411", "412", "413", "414", "415" };
            return Array.IndexOf(st, fim) >= 0;
        }
    }
}
