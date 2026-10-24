using System.Drawing;
using System.Windows.Forms;

namespace CorrigirNFe
{
    /// <summary>Cores, fontes e fabricas de controles: um lugar so para a aparencia.</summary>
    internal static class Ui
    {
        public static readonly Font Fonte = new Font("Segoe UI", 9.5f);
        public static readonly Font FonteNegrito = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font FontePequena = new Font("Segoe UI", 8.5f);
        public static readonly Font FonteTitulo = new Font("Segoe UI Semibold", 14f);
        public static readonly Font FonteSubtitulo = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        public static readonly Font FonteBotaoGrande = new Font("Segoe UI", 11f, FontStyle.Bold);

        public static readonly Color Fundo = Color.FromArgb(247, 248, 250);
        public static readonly Color Painel = Color.White;
        public static readonly Color Texto = Color.FromArgb(31, 41, 55);
        public static readonly Color Discreto = Color.FromArgb(107, 114, 128);
        public static readonly Color Borda = Color.FromArgb(222, 226, 231);

        public static readonly Color Primaria = Color.FromArgb(21, 101, 192);
        public static readonly Color Verde = Color.FromArgb(22, 128, 61);
        public static readonly Color VerdeFundo = Color.FromArgb(232, 245, 236);
        public static readonly Color Vermelho = Color.FromArgb(176, 30, 35);
        public static readonly Color VermelhoFundo = Color.FromArgb(253, 236, 236);
        public static readonly Color Ambar = Color.FromArgb(146, 94, 0);
        public static readonly Color AmbarFundo = Color.FromArgb(255, 246, 219);

        public static Label Rotulo(string texto, Font fonte, Color cor)
        {
            return new Label
            {
                Text = texto,
                Font = fonte,
                ForeColor = cor,
                AutoSize = true,
                Margin = new Padding(3, 3, 3, 3)
            };
        }

        public static Button Botao(string texto, bool primario)
        {
            var b = new Button
            {
                Text = texto,
                AutoSize = true,
                MinimumSize = new Size(110, 34),
                Padding = new Padding(10, 2, 10, 2),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderColor = primario ? Primaria : Borda;
            b.BackColor = primario ? Primaria : Color.White;
            b.ForeColor = primario ? Color.White : Texto;
            return b;
        }

        public static Color CorSituacao(Situacao s)
        {
            switch (s)
            {
                case Situacao.PrecisaCorrigir: return Vermelho;
                case Situacao.ComObservacoes: return Ambar;
                case Situacao.Ok: return Verde;
                default: return Discreto;
            }
        }

        public static Color FundoSituacao(Situacao s)
        {
            switch (s)
            {
                case Situacao.PrecisaCorrigir: return VermelhoFundo;
                default: return Painel;
            }
        }
    }
}
