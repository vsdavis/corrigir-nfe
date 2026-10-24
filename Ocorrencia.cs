using System.Collections.Generic;

namespace CorrigirNFe
{
    /// <summary>Natureza de cada achado, que decide em que bloco do relatorio ele sai.</summary>
    internal enum Tipo
    {
        /// <summary>Estrutura: ordem de tags, tag vazia, grupo indevido. Corrigido automaticamente.</summary>
        Estrutura,

        /// <summary>Valor que nao bate com a aritmetica dos outros campos declarados. Corrigido.</summary>
        Calculo,

        /// <summary>Depende de enquadramento fiscal. NUNCA alterado: so reportado.</summary>
        Fiscal,

        /// <summary>Problema que o programa nao sabe resolver sozinho. Exige alguem olhar.</summary>
        Bloqueio
    }

    internal sealed class Ocorrencia
    {
        public Tipo Tipo;
        public string Local;
        public string Descricao;
        public bool Corrigido;

        public Ocorrencia(Tipo tipo, string local, string descricao, bool corrigido)
        {
            Tipo = tipo;
            Local = local;
            Descricao = descricao;
            Corrigido = corrigido;
        }
    }

    internal sealed class Resultado
    {
        /// <summary>false = nao deu para processar o arquivo (ver Erro).</summary>
        public bool Ok;
        public string Erro;

        public string Arquivo;
        public string Chave;
        public string NumeroNF;
        public string Emitente;
        public int QtdItens;
        public bool TinhaAssinatura;

        public readonly List<Ocorrencia> Ocorrencias = new List<Ocorrencia>();

        public void Add(Tipo t, string local, string desc, bool corrigido)
        {
            Ocorrencias.Add(new Ocorrencia(t, local, desc, corrigido));
        }

        public int Contar(Tipo t)
        {
            int n = 0;
            foreach (Ocorrencia o in Ocorrencias) if (o.Tipo == t) n++;
            return n;
        }

        /// <summary>Houve alguma alteracao efetiva no XML?</summary>
        public bool Alterou
        {
            get
            {
                foreach (Ocorrencia o in Ocorrencias) if (o.Corrigido) return true;
                return false;
            }
        }

        /// <summary>Existe algo que o programa nao resolveu e precisa de gente?</summary>
        public bool TemBloqueio { get { return Contar(Tipo.Bloqueio) > 0; } }
    }
}
