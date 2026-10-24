using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace CorrigirNFe
{
    /// <summary>Tela de configuracao: pasta das notas e pasta de backup.</summary>
    internal sealed class FormConfig : Form
    {
        private readonly Config _cfg;
        private readonly TextBox _txtNotas = new TextBox();
        private readonly TextBox _txtBackup = new TextBox();

        public FormConfig(Config cfg, bool primeiraVez)
        {
            _cfg = cfg;

            Text = "Configurações - Corrigir NF-e";
            Font = Ui.Fonte;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = primeiraVez;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Ui.Fundo;

            // A janela se ajusta ao conteudo: altura certa em qualquer DPI/fonte.
            var tl = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(640, 0),
                Padding = new Padding(18, 14, 18, 14),
                ColumnCount = 2,
                RowCount = 8
            };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(tl);

            if (primeiraVez)
            {
                var boasVindas = Ui.Rotulo("Antes de começar, informe as duas pastas abaixo. "
                    + "Isso só precisa ser feito uma vez nesta máquina.", Ui.Fonte, Ui.Texto);
                boasVindas.MaximumSize = new Size(600, 0);
                boasVindas.Margin = new Padding(0, 0, 0, 12);
                tl.Controls.Add(boasVindas, 0, 0);
                tl.SetColumnSpan(boasVindas, 2);
            }

            tl.Controls.Add(Ui.Rotulo("Pasta onde o sistema grava os XMLs das notas", Ui.FonteNegrito, Ui.Texto), 0, 1);
            var dica1 = Ui.Rotulo("É dela que o programa lê as notas para corrigir.", Ui.FontePequena, Ui.Discreto);
            tl.Controls.Add(dica1, 0, 2);
            _txtNotas.Dock = DockStyle.Fill;
            _txtNotas.Text = cfg.PastaNotas;
            tl.Controls.Add(_txtNotas, 0, 3);
            tl.Controls.Add(BotaoProcurar(_txtNotas, "Escolha a pasta onde o sistema grava os XMLs das notas"), 1, 3);

            var t2 = Ui.Rotulo("Pasta de backup", Ui.FonteNegrito, Ui.Texto);
            t2.Margin = new Padding(3, 16, 3, 0);
            tl.Controls.Add(t2, 0, 4);
            tl.Controls.Add(Ui.Rotulo("Antes de qualquer correção, o XML original é copiado para cá.",
                Ui.FontePequena, Ui.Discreto), 0, 5);
            _txtBackup.Dock = DockStyle.Fill;
            _txtBackup.Text = string.IsNullOrEmpty(cfg.PastaBackup) ? BackupSugerido() : cfg.PastaBackup;
            tl.Controls.Add(_txtBackup, 0, 6);
            tl.Controls.Add(BotaoProcurar(_txtBackup, "Escolha a pasta de backup"), 1, 6);

            var botoes = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true,
                Margin = new Padding(0, 20, 0, 0)
            };
            var salvar = Ui.Botao("Salvar", true);
            var cancelar = Ui.Botao("Cancelar", false);
            salvar.Click += Salvar;
            cancelar.DialogResult = DialogResult.Cancel;
            botoes.Controls.Add(salvar);
            botoes.Controls.Add(cancelar);
            tl.Controls.Add(botoes, 0, 7);
            tl.SetColumnSpan(botoes, 2);

            AcceptButton = salvar;
            CancelButton = cancelar;
        }

        private static string BackupSugerido()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                "Corrigir NF-e - Backup");
        }

        private Button BotaoProcurar(TextBox alvo, string descricao)
        {
            var b = Ui.Botao("Procurar...", false);
            b.Margin = new Padding(6, 0, 0, 0);
            b.Click += delegate
            {
                using (var d = new FolderBrowserDialog())
                {
                    d.Description = descricao;
                    d.ShowNewFolderButton = true;
                    if (Directory.Exists(alvo.Text)) d.SelectedPath = alvo.Text;
                    if (d.ShowDialog(this) == DialogResult.OK) alvo.Text = d.SelectedPath;
                }
            };
            return b;
        }

        private void Salvar(object sender, EventArgs e)
        {
            string notas = _txtNotas.Text.Trim().Trim('"');
            string backup = _txtBackup.Text.Trim().Trim('"');

            string erro = Config.Validar(notas, backup);
            if (erro != null)
            {
                MessageBox.Show(this, erro, "Configurações", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _cfg.PastaNotas = notas;
            _cfg.PastaBackup = backup;
            try { _cfg.Salvar(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não consegui salvar a configuração:\n" + ex.Message, "Configurações",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
