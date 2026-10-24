using System.Collections.Generic;

namespace CorrigirNFe
{
    /// <summary>
    /// Tabelas extraidas do schema oficial da NF-e (PL_010_V1.30:
    /// leiauteNFe_v4.00.xsd + DFeTiposBasicos_v1.00.xsd).
    /// </summary>
    internal static class Layout
    {
        /// <summary>
        /// Ordem canonica dos filhos de cada grupo. Grupos cujo nome aparece no
        /// schema com conteudos diferentes conforme o contexto (IPI, gIBS, gIBSUF,
        /// gIBSMun, gCBS) ficam de fora de proposito: reordenar pelo nome poderia
        /// embaralhar o grupo errado.
        /// </summary>
        internal static readonly Dictionary<string, string[]> Ordem = new Dictionary<string, string[]>
        {
            { "infNFe", new[] { "ide", "emit", "avulsa", "dest", "retirada", "entrega", "autXML", "det", "total", "transp", "cobr", "pag", "infIntermed", "infAdic", "exporta", "compra", "cana", "infRespTec", "infSolicNFF", "agropecuario", "infPAA" } },
            { "ide", new[] { "cUF", "cNF", "natOp", "mod", "serie", "nNF", "dhEmi", "dhSaiEnt", "dPrevEntrega", "tpNF", "idDest", "cMunFG", "cMunFGIBS", "tpImp", "tpEmis", "cDV", "tpAmb", "finNFe", "tpNFDebito", "tpNFCredito", "indFinal", "indPres", "indIntermed", "cIndOp", "procEmi", "verProc", "dhCont", "xJust", "NFref", "gCompraGov", "gPagAntecipado" } },
            { "emit", new[] { "CNPJ", "CPF", "xNome", "xFant", "enderEmit", "IE", "IEST", "IM", "CNAE", "CRT", "ISUFEmit" } },
            { "enderEmit", new[] { "xLgr", "nro", "xCpl", "xBairro", "cMun", "xMun", "UF", "CEP", "cPais", "xPais", "fone" } },
            { "dest", new[] { "CNPJ", "CPF", "idEstrangeiro", "xNome", "enderDest", "indIEDest", "IE", "ISUF", "IM", "email" } },
            { "enderDest", new[] { "xLgr", "nro", "xCpl", "xBairro", "cMun", "xMun", "UF", "CEP", "cPais", "xPais", "fone" } },
            { "entrega", new[] { "CNPJ", "CPF", "xNome", "xLgr", "nro", "xCpl", "xBairro", "cMun", "xMun", "UF", "CEP", "cPais", "xPais", "fone", "email", "IE" } },
            { "retirada", new[] { "CNPJ", "CPF", "xNome", "xLgr", "nro", "xCpl", "xBairro", "cMun", "xMun", "UF", "CEP", "cPais", "xPais", "fone", "email", "IE" } },
            { "det", new[] { "prod", "imposto", "impostoDevol", "infAdProd", "obsItem", "vItem", "DFeReferenciado" } },
            { "prod", new[] { "cProd", "cEAN", "cBarra", "xProd", "NCM", "NVE", "CEST", "indEscala", "CNPJFab", "cBenef", "gCred", "tpCredPresIBSZFM", "EXTIPI", "CFOP", "uCom", "qCom", "vUnCom", "vProd", "cEANTrib", "cBarraTrib", "uTrib", "qTrib", "vUnTrib", "vFrete", "vSeg", "vDesc", "vOutro", "indTot", "indBemMovelUsado", "DI", "detExport", "xPed", "nItemPed", "nFCI", "rastro", "infProdNFF", "infProdEmb", "veicProd", "med", "arma", "comb", "nRECOPI" } },
            { "imposto", new[] { "vTotTrib", "ICMS", "IPI", "II", "ISSQN", "PIS", "PISST", "COFINS", "COFINSST", "ICMSUFDest", "IS", "IBSCBS" } },
            { "total", new[] { "ICMSTot", "ISSQNtot", "retTrib", "ISTot", "IBSCBSTot", "vNFTot" } },
            { "ICMSTot", new[] { "vBC", "vICMS", "vICMSDeson", "vFCPUFDest", "vICMSUFDest", "vICMSUFRemet", "vFCP", "vBCST", "vST", "vFCPST", "vFCPSTRet", "qBCMono", "vICMSMono", "qBCMonoReten", "vICMSMonoReten", "qBCMonoRet", "vICMSMonoRet", "vProd", "vFrete", "vSeg", "vDesc", "vII", "vIPI", "vIPIDevol", "vPIS", "vCOFINS", "vOutro", "vNF", "vTotTrib" } },
            { "IBSCBSTot", new[] { "vBCIBSCBS", "gIBS", "gCBS", "gMono", "gEstornoCred" } },
            { "gIBSCBS", new[] { "vBC", "gIBSUF", "gIBSMun", "vIBS", "gCBS", "gTribRegular", "gTribCompraGov" } },
            { "IBSCBS", new[] { "CST", "cClassTrib", "indDoacao", "gIBSCBS", "gIBSCBSMono", "gTransfCred", "gAjusteCompet", "gEstornoCred", "gCredPresOper", "gCredPresIBSZFM" } },
            { "ICMS00", new[] { "orig", "CST", "modBC", "vBC", "pICMS", "vICMS", "pFCP", "vFCP" } },
            { "ICMS10", new[] { "orig", "CST", "modBC", "vBC", "pICMS", "vICMS", "vBCFCP", "pFCP", "vFCP", "modBCST", "pMVAST", "pRedBCST", "vBCST", "pICMSST", "vICMSST", "vBCFCPST", "pFCPST", "vFCPST", "vICMSSTDeson", "motDesICMSST" } },
            { "ICMS20", new[] { "orig", "CST", "modBC", "pRedBC", "vBC", "pICMS", "vICMS", "vBCFCP", "pFCP", "vFCP", "vICMSDeson", "motDesICMS", "indDeduzDeson" } },
            { "ICMS40", new[] { "orig", "CST", "vICMSDeson", "motDesICMS", "indDeduzDeson" } },
            { "ICMS51", new[] { "orig", "CST", "modBC", "pRedBC", "cBenefRBC", "vBC", "pICMS", "vICMSOp", "pDif", "vICMSDif", "vICMS", "vBCFCP", "pFCP", "vFCP", "pFCPDif", "vFCPDif", "vFCPEfet" } },
            { "ICMS60", new[] { "orig", "CST", "vBCSTRet", "pST", "vICMSSubstituto", "vICMSSTRet", "vBCFCPSTRet", "pFCPSTRet", "vFCPSTRet", "pRedBCEfet", "vBCEfet", "pICMSEfet", "vICMSEfet" } },
            { "ICMS70", new[] { "orig", "CST", "modBC", "pRedBC", "vBC", "pICMS", "vICMS", "vBCFCP", "pFCP", "vFCP", "modBCST", "pMVAST", "pRedBCST", "vBCST", "pICMSST", "vICMSST", "vBCFCPST", "pFCPST", "vFCPST", "vICMSDeson", "motDesICMS", "indDeduzDeson", "vICMSSTDeson", "motDesICMSST" } },
            { "ICMS90", new[] { "orig", "CST", "modBC", "vBC", "pRedBC", "cBenefRBC", "pICMS", "vICMSOp", "pDif", "vICMSDif", "vICMS", "vBCFCP", "pFCP", "vFCP", "pFCPDif", "vFCPDif", "vFCPEfet", "modBCST", "pMVAST", "pRedBCST", "vBCST", "pICMSST", "vICMSST", "vBCFCPST", "pFCPST", "vFCPST", "vICMSDeson", "motDesICMS", "indDeduzDeson", "vICMSSTDeson", "motDesICMSST" } },
            { "IPINT", new[] { "CST" } },
            { "IPITrib", new[] { "CST", "vBC", "pIPI", "qUnid", "vUnid", "vIPI" } },
            { "PISOutr", new[] { "CST", "vBC", "pPIS", "qBCProd", "vAliqProd", "vPIS" } },
            { "PISAliq", new[] { "CST", "vBC", "pPIS", "vPIS" } },
            { "COFINSOutr", new[] { "CST", "vBC", "pCOFINS", "qBCProd", "vAliqProd", "vCOFINS" } },
            { "COFINSAliq", new[] { "CST", "vBC", "pCOFINS", "vCOFINS" } },
            { "transp", new[] { "modFrete", "transporta", "retTransp", "veicTransp", "reboque", "vagao", "balsa", "vol" } },
            { "pag", new[] { "detPag", "vTroco" } },
            { "detPag", new[] { "indPag", "tPag", "xPag", "vPag", "dPag", "CNPJPag", "UFPag", "card" } },
            { "infRespTec", new[] { "CNPJ", "xContato", "email", "fone", "idCSRT", "hashCSRT" } },
        };

        /// <summary>
        /// Tags opcionais cujo tipo rejeita conteudo vazio: se vierem em branco a
        /// nota nao valida, e a correcao e remover. Verificado contra o pattern /
        /// minLength de cada uma no schema.
        /// </summary>
        internal static readonly HashSet<string> RemoverSeVazia = new HashSet<string>
        {
            "CEST", "xPed", "nItemPed", "xCpl", "NVE", "EXTIPI", "indEscala",
            "CNPJFab", "nFCI", "xFant", "IEST", "CNAE", "ISUF",
            "infAdProd", "infCpl", "infAdFisco"
        };

        /// <summary>
        /// Tags que sao opcionais em alguns grupos e obrigatorias em outros. So
        /// podem ser removidas quando vazias dentro do pai listado aqui. Ex.: email
        /// vazio em <dest> sai, mas em <infRespTec> e obrigatorio e fica (vira aviso).
        /// </summary>
        internal static readonly HashSet<string> RemoverSeVaziaPorPai = new HashSet<string>
        {
            "dest/email", "entrega/email", "retirada/email",
            "emit/IM", "dest/IM",
            "enderEmit/fone", "enderDest/fone", "entrega/fone", "retirada/fone"
        };

        /// <summary>
        /// Tags cujo tipo ACEITA conteudo vazio: deixar como estao. cEAN e cEANTrib
        /// vazias sao validas (pattern inclui [0-9]{0}); o valor "SEM GTIN" tambem.
        /// </summary>
        internal static readonly HashSet<string> VazioEhValido = new HashSet<string>
        {
            "cEAN", "cEANTrib", "cBenef"
        };

        /// <summary>Grupos opcionais que devem sumir inteiros se vierem todos em branco.</summary>
        internal static readonly string[] GruposOpcionais = { "entrega", "retirada" };
    }
}
