using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;

namespace CorrigirNFe
{
    internal enum Estilo { Titulo, Subtitulo, Normal, Item, Ok, Alerta, Discreto }

    internal sealed class Trecho
    {
        public string Texto;
        public Estilo Estilo;
        public Trecho(string t, Estilo e) { Texto = t; Estilo = e; }
    }

    /// <summary>
    /// Monta o texto do painel de detalhes. Primeiro um resumo para quem nao conhece
    /// o layout da NF-e (o cliente), depois os detalhes tecnicos (o suporte).
    /// </summary>
    internal static class Relatorio
    {
        public static List<Trecho> Montar(Analise a)
        {
            var t = new List<Trecho>();

            if (a.Situacao == Situacao.Erro)
            {
                t.Add(new Trecho("Não foi possível analisar este arquivo", Estilo.Titulo));
                t.Add(new Trecho(a.Erro, Estilo.Alerta));
                t.Add(new Trecho("Se este arquivo é mesmo uma nota, envie-o para o suporte.", Estilo.Normal));
                return t;
            }
            if (a.Situacao == Situacao.Autorizada)
            {
                t.Add(new Trecho("Nota já autorizada", Estilo.Titulo));
                t.Add(new Trecho(a.Erro, Estilo.Normal));
                return t;
            }

            Resultado r = a.R;
            var corrigir = r.Ocorrencias.Where(o => o.Tipo == Tipo.Estrutura || o.Tipo == Tipo.Calculo).ToList();
            var fiscal = r.Ocorrencias.Where(o => o.Tipo == Tipo.Fiscal).ToList();
            var bloqueio = r.Ocorrencias.Where(o => o.Tipo == Tipo.Bloqueio).ToList();

            // ---- resumo ----
            if (a.Situacao == Situacao.PrecisaCorrigir)
            {
                t.Add(new Trecho("Esta nota precisa de correção", Estilo.Titulo));
                t.Add(new Trecho(string.Format(
                    "Encontrei {0} ponto(s) que fazem a SEFAZ rejeitar a nota. Todos são corrigidos "
                  + "automaticamente ao clicar em \"Corrigir nota\". Antes de alterar, o arquivo original "
                  + "é guardado na pasta de backup.", corrigir.Count), Estilo.Normal));
                t.Add(new Trecho("Depois de corrigir, volte ao sistema e transmita a nota novamente.", Estilo.Normal));
            }
            else
            {
                t.Add(new Trecho("Nada a corrigir nesta nota", Estilo.Ok));
                t.Add(new Trecho(a.Situacao == Situacao.ComObservacoes
                    ? "O arquivo está de acordo com o layout e os cálculos batem. Há apenas observações "
                    + "abaixo para o contador conferir, que não impedem a transmissão."
                    : "O arquivo está de acordo com o layout e os cálculos batem. Se a nota foi rejeitada, "
                    + "o motivo é outro: chame o suporte e informe a mensagem de rejeição.", Estilo.Normal));
            }

            if (r.TinhaAssinatura)
                t.Add(new Trecho("Este XML estava assinado. A assinatura é removida na correção e o sistema "
                               + "precisa assinar de novo ao transmitir.", Estilo.Alerta));

            if (bloqueio.Count > 0)
            {
                t.Add(new Trecho("Precisa de atenção do suporte  (não é alterado pelo programa)", Estilo.Subtitulo));
                Itens(t, bloqueio);
            }

            // ---- detalhes ----
            var valores = corrigir.Where(o => o.Tipo == Tipo.Calculo).ToList();
            if (valores.Count > 0)
            {
                t.Add(new Trecho("Valores que serão recalculados", Estilo.Subtitulo));
                // Totais primeiro: e o que o cliente reconhece da mensagem de rejeicao.
                Itens(t, valores.Where(o => o.Local == "total").Concat(valores.Where(o => o.Local != "total")).ToList());
            }

            var estrutura = corrigir.Where(o => o.Tipo == Tipo.Estrutura).ToList();
            if (estrutura.Count > 0)
            {
                t.Add(new Trecho("Ajustes no formato do arquivo", Estilo.Subtitulo));
                Itens(t, estrutura);
            }

            if (fiscal.Count > 0)
            {
                t.Add(new Trecho("Para o contador conferir  (não é alterado pelo programa)", Estilo.Subtitulo));
                Itens(t, fiscal);
            }

            return t;
        }

        private static readonly NumberFormatInfo Br = new NumberFormatInfo
        {
            NumberDecimalSeparator = ",",
            NumberGroupSeparator = ".",
            NumberDecimalDigits = 2
        };

        /// <summary>"1514.05 -> 1514.06" vira "1.514,05 -> 1.514,06" (e 0.9000% vira 0,9000%), como o cliente le no sistema.</summary>
        internal static string ValoresBr(string s)
        {
            return Regex.Replace(s, @"(?<![\d.,])\d+\.(\d{2,4})(?![\d.])", m =>
            {
                decimal v;
                return decimal.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out v)
                    ? v.ToString("N" + m.Groups[1].Value.Length, Br) : m.Value;
            });
        }

        private static void Itens(List<Trecho> t, List<Ocorrencia> lista)
        {
            // Grupos que se repetem por item (prod, ICMS00...) viram uma linha com contagem.
            foreach (var g in lista.GroupBy(o => o.Local + "\u0001" + o.Descricao))
            {
                Ocorrencia o = g.First();
                int n = g.Count();
                string local = string.IsNullOrEmpty(o.Local) ? "" : o.Local + ": ";
                string desc = o.Tipo == Tipo.Calculo ? ValoresBr(o.Descricao) : o.Descricao;
                t.Add(new Trecho("•  " + local + desc + (n > 1 ? "  [" + n + "x]" : ""), Estilo.Item));
            }
        }
    }
}
