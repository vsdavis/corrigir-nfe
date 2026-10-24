using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CorrigirNFe
{
    internal enum Situacao
    {
        /// <summary>Tem algo a corrigir: o botao Corrigir fica habilitado.</summary>
        PrecisaCorrigir,
        /// <summary>Nada a corrigir, mas ha pontos fiscais/manuais para conferir.</summary>
        ComObservacoes,
        /// <summary>Nada a corrigir.</summary>
        Ok,
        /// <summary>nfeProc: nota ja autorizada pela SEFAZ.</summary>
        Autorizada,
        /// <summary>Nao deu para ler (nao e NF-e, XML quebrado, arquivo em uso...).</summary>
        Erro
    }

    internal sealed class Analise
    {
        public string Caminho;
        public string Nome;
        public DateTime Modificado;
        public Situacao Situacao;
        public Resultado R;
        public string Erro;

        public string TextoSituacao
        {
            get
            {
                switch (Situacao)
                {
                    case Situacao.PrecisaCorrigir: return "Precisa corrigir";
                    case Situacao.ComObservacoes: return "OK (ver observações)";
                    case Situacao.Ok: return "OK";
                    case Situacao.Autorizada: return "Já autorizada";
                    default: return "Não foi possível ler";
                }
            }
        }
    }

    internal sealed class Retorno
    {
        public bool Sucesso;
        public string Mensagem;
        public string Backup;
        public Analise Depois;
    }

    /// <summary>
    /// Operacoes de arquivo da tela: analisar, corrigir com backup, restaurar.
    /// Toda a regra fiscal continua em Corretor; aqui so ha leitura/gravacao segura.
    /// </summary>
    internal static class Servico
    {
        // ---------------- analise ----------------

        public static Analise Analisar(string caminho, string indPres)
        {
            var a = new Analise { Caminho = caminho, Nome = Path.GetFileName(caminho) };
            try { a.Modificado = File.GetLastWriteTime(caminho); } catch { }

            string erro;
            XDocument doc = Corretor.Carregar(caminho, out erro);
            if (doc == null)
            {
                a.Situacao = Situacao.Erro;
                a.Erro = Primeira(erro);
                return a;
            }

            var c = new Corretor();
            c.IndPresPadrao = indPres;
            Resultado r = c.Processar(caminho, doc);
            a.R = r;

            if (doc.Root != null && doc.Root.Name.LocalName == "nfeProc")
            {
                a.Situacao = Situacao.Autorizada;
                a.Erro = "Este XML é de uma nota JÁ AUTORIZADA pela SEFAZ. Nota autorizada não se corrige: "
                       + "o caminho é cancelamento ou carta de correção.";
                return a;
            }
            if (!r.Ok)
            {
                a.Situacao = Situacao.Erro;
                a.Erro = Primeira(r.Erro);
                return a;
            }

            if (r.Alterou) a.Situacao = Situacao.PrecisaCorrigir;
            else if (r.Contar(Tipo.Fiscal) + r.Contar(Tipo.Bloqueio) > 0) a.Situacao = Situacao.ComObservacoes;
            else a.Situacao = Situacao.Ok;
            return a;
        }

        private static string Primeira(string s)
        {
            if (string.IsNullOrEmpty(s)) return "erro desconhecido";
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>Lista os XMLs da pasta (sem subpastas), do mais recente para o mais antigo.</summary>
        public static List<string> ListarXmls(string pasta, int max)
        {
            if (string.IsNullOrEmpty(pasta) || !Directory.Exists(pasta)) return new List<string>();
            return new DirectoryInfo(pasta)
                .GetFiles("*.xml", SearchOption.TopDirectoryOnly)
                .Where(f => !f.Name.EndsWith(".corrigida.xml", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTime)
                .Take(max)
                .Select(f => f.FullName)
                .ToList();
        }

        // ---------------- correcao ----------------

        /// <summary>
        /// Corrige o arquivo no proprio lugar, para o sistema emissor retransmitir.
        /// Ordem: (1) le de novo do disco, (2) copia o original para o backup e confere,
        /// (3) grava num .tmp ao lado, (4) troca o original pelo .tmp, (5) reanalisa.
        /// Se qualquer passo antes do (4) falhar, o original fica intocado.
        /// </summary>
        public static Retorno Corrigir(string caminho, Config cfg)
        {
            var ret = new Retorno();

            // (1) Reler: o arquivo pode ter mudado desde que a lista foi montada.
            string erro;
            XDocument doc = Corretor.Carregar(caminho, out erro);
            if (doc == null) { ret.Mensagem = "Não consegui ler a nota: " + erro; return ret; }

            var c = new Corretor();
            c.IndPresPadrao = cfg.IndPres;
            Resultado r = c.Processar(caminho, doc);
            if (!r.Ok) { ret.Mensagem = Primeira(r.Erro); return ret; }
            if (!r.Alterou)
            {
                ret.Sucesso = true;
                ret.Mensagem = "Esta nota não tem nada a corrigir.";
                ret.Depois = Analisar(caminho, cfg.IndPres);
                return ret;
            }

            // (2) Backup do original, antes de qualquer alteracao.
            string backup;
            try { backup = FazerBackup(caminho, cfg.PastaBackup); }
            catch (Exception ex)
            {
                ret.Mensagem = "Não consegui fazer o backup, por isso NÃO alterei a nota.\n\n" + ex.Message;
                return ret;
            }
            ret.Backup = backup;

            // (3) + (4) Grava ao lado e troca. File.Replace na mesma pasta e atomico no NTFS:
            // o sistema emissor nunca ve um arquivo pela metade.
            string tmp = caminho + ".corrigindo.tmp";
            try
            {
                Gravar(doc, tmp);
                Trocar(tmp, caminho);
            }
            catch (IOException ex)
            {
                TentarApagar(tmp);
                ret.Mensagem = "A nota está aberta em outro programa (provavelmente na tela de transmissão do sistema). "
                             + "Feche essa tela e clique em Corrigir de novo.\n\nNada foi alterado.\n\n(" + ex.Message + ")";
                return ret;
            }
            catch (Exception ex)
            {
                TentarApagar(tmp);
                ret.Mensagem = "Não consegui gravar a nota corrigida. Nada foi alterado.\n\n" + ex.Message;
                return ret;
            }

            // (5) Confere o que ficou gravado.
            ret.Depois = Analisar(caminho, cfg.IndPres);
            ret.Sucesso = ret.Depois.Situacao != Situacao.PrecisaCorrigir && ret.Depois.Situacao != Situacao.Erro;
            if (!ret.Sucesso)
                ret.Mensagem = "A nota foi gravada, mas a conferência final ainda encontrou problemas. "
                             + "Use Restaurar original e chame o suporte.";

            Log(cfg.PastaBackup, ret.Sucesso ? "CORRIGIDA" : "CORRIGIDA-COM-FALHA", caminho, r.NumeroNF,
                "backup: " + backup);
            return ret;
        }

        private static string FazerBackup(string caminho, string pastaBackup)
        {
            DateTime agora = DateTime.Now;
            string dir = Path.Combine(pastaBackup, agora.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(dir);

            string nome = Path.GetFileName(caminho);
            string destino = Path.Combine(dir, agora.ToString("HHmmss") + "_" + nome);
            int n = 2;
            while (File.Exists(destino))
                destino = Path.Combine(dir, agora.ToString("HHmmss") + "-" + (n++) + "_" + nome);

            File.Copy(caminho, destino, false);

            if (new FileInfo(destino).Length != new FileInfo(caminho).Length)
                throw new IOException("a cópia de backup ficou com tamanho diferente do original");
            return destino;
        }

        /// <summary>UTF-8 sem BOM e sem indentacao: o formato que o emissor espera e assina.</summary>
        private static void Gravar(XDocument doc, string destino)
        {
            var cfg = new XmlWriterSettings
            {
                Indent = false,
                OmitXmlDeclaration = false,
                Encoding = new UTF8Encoding(false),
                NewLineHandling = NewLineHandling.None
            };
            using (var w = XmlWriter.Create(destino, cfg))
                doc.Save(w);
        }

        // ---------------- restauracao ----------------

        /// <summary>Backup mais recente deste arquivo, ou null.</summary>
        public static string UltimoBackup(string caminho, string pastaBackup)
        {
            if (string.IsNullOrEmpty(pastaBackup) || !Directory.Exists(pastaBackup)) return null;
            string nome = Path.GetFileName(caminho);
            try
            {
                // Pastas yyyy-MM-dd e prefixo HHmmss: a ordem do caminho e a ordem do tempo.
                return Directory.GetFiles(pastaBackup, "*_" + nome, SearchOption.AllDirectories)
                                .Where(f => EhBackupDe(Path.GetFileName(f), nome))
                                .OrderByDescending(f => f, StringComparer.Ordinal)
                                .FirstOrDefault();
            }
            catch { return null; }
        }

        /// <summary>"HHmmss_nome" ou "HHmmss-N_nome" -- e nao "HHmmss_outro_nome".</summary>
        private static bool EhBackupDe(string arquivoBackup, string nome)
        {
            if (!arquivoBackup.EndsWith("_" + nome, StringComparison.OrdinalIgnoreCase)) return false;
            string prefixo = arquivoBackup.Substring(0, arquivoBackup.Length - nome.Length - 1);
            return System.Text.RegularExpressions.Regex.IsMatch(prefixo, @"^\d{6}(-\d+)?$");
        }

        public static Retorno Restaurar(string caminho, Config cfg)
        {
            var ret = new Retorno();
            string b = UltimoBackup(caminho, cfg.PastaBackup);
            if (b == null) { ret.Mensagem = "Não há backup desta nota."; return ret; }

            string tmp = caminho + ".restaurando.tmp";
            try
            {
                File.Copy(b, tmp, true);
                if (File.Exists(caminho)) Trocar(tmp, caminho);
                else File.Move(tmp, caminho);
            }
            catch (Exception ex)
            {
                TentarApagar(tmp);
                ret.Mensagem = "Não consegui restaurar. A nota pode estar aberta em outro programa.\n\n" + ex.Message;
                return ret;
            }

            ret.Sucesso = true;
            ret.Backup = b;
            ret.Depois = Analisar(caminho, cfg.IndPres);
            Log(cfg.PastaBackup, "RESTAURADA", caminho,
                ret.Depois.R != null ? ret.Depois.R.NumeroNF : null, "de: " + b);
            return ret;
        }

        // ---------------- apoio ----------------

        /// <summary>
        /// Troca o destino pelo .tmp. File.Replace e atomico no disco local; algumas pastas
        /// de rede/NAS nao suportam, e ai cai para uma copia simples por cima. Se o arquivo
        /// estiver travado por outro programa, a copia tambem falha e a excecao sobe.
        /// </summary>
        private static void Trocar(string tmp, string destino)
        {
            try { File.Replace(tmp, destino, null); }
            catch (Exception ex) when (ex is IOException || ex is PlatformNotSupportedException)
            {
                File.Copy(tmp, destino, true);
                File.Delete(tmp);
            }
        }

        private static void TentarApagar(string f)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        }

        /// <summary>Historico simples na pasta de backup, para o suporte saber o que foi feito e por quem.</summary>
        private static void Log(string pastaBackup, string acao, string arquivo, string nf, string extra)
        {
            try
            {
                string linha = string.Join(" | ", new[]
                {
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    acao,
                    "NF " + (string.IsNullOrEmpty(nf) ? "?" : nf),
                    Path.GetFileName(arquivo),
                    "usuario: " + Environment.UserName + "@" + Environment.MachineName,
                    extra
                });
                File.AppendAllText(Path.Combine(pastaBackup, "historico.log"),
                                   linha + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { /* log nunca impede a correcao */ }
        }
    }
}
