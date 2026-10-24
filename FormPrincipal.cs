using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CorrigirNFe
{
    internal sealed class FormPrincipal : Form
    {
        private readonly Config _cfg;

        // topo
        private readonly Label _lblPasta = new Label();
        // lista
        private readonly TextBox _txtBusca = new TextBox();
        private readonly CheckBox _chkSoCorrigir = new CheckBox();
        private readonly ListView _lista = new ListView();
        // detalhes
        private readonly Label _lblNF = new Label();
        private readonly Label _lblEmit = new Label();
        private readonly Label _lblChave = new Label();
        private readonly RichTextBox _rtb = new RichTextBox();
        private Button _btnCorrigir, _btnRestaurar, _btnBackup;
        // rodape
        private readonly ToolStripStatusLabel _status = new ToolStripStatusLabel();

        private List<Analise> _analises = new List<Analise>();
        private Dictionary<string, Analise> _cache = new Dictionary<string, Analise>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Arquivos de fora da pasta configurada (arrastados para a janela).</summary>
        private readonly List<string> _extras = new List<string>();

        private Analise _atual;
        private int _geracao;
        private FileSystemWatcher _vigia;
        private readonly Timer _espera = new Timer { Interval = 1200 };

        public FormPrincipal(Config cfg, string arquivoInicial)
        {
            _cfg = cfg;
            if (!string.IsNullOrEmpty(arquivoInicial) && File.Exists(arquivoInicial))
                _extras.Add(Path.GetFullPath(arquivoInicial));

            Text = "Corrigir NF-e";
            Font = Ui.Fonte;
            BackColor = Ui.Fundo;
            ClientSize = new Size(1180, 700);
            MinimumSize = new Size(940, 560);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            MontarTela();

            _espera.Tick += delegate { _espera.Stop(); Recarregar(null); };
            DragEnter += AoArrastar;
            DragDrop += AoSoltar;
        }

        // =====================================================================
        // montagem
        // =====================================================================

        private void MontarTela()
        {
            var raiz = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            raiz.Controls.Add(MontarTopo(), 0, 0);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = Ui.Borda,
                SplitterWidth = 1,
                Margin = new Padding(0)
            };
            split.Panel1.BackColor = Ui.Painel;
            split.Panel2.BackColor = Ui.Painel;
            split.Panel1.Controls.Add(MontarLista());
            split.Panel2.Controls.Add(MontarDetalhes());
            raiz.Controls.Add(split, 0, 1);

            var rodape = new StatusStrip { SizingGrip = true, BackColor = Ui.Fundo };
            _status.Spring = true;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            rodape.Items.Add(_status);

            // Ordem importa no WinForms: o Fill entra primeiro.
            Controls.Add(raiz);
            Controls.Add(rodape);

            Load += delegate { split.SplitterDistance = (int)(ClientSize.Width * 0.50); };
        }

        private Control MontarTopo()
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 4,
                Padding = new Padding(14, 10, 14, 10),
                BackColor = Ui.Painel,
                Margin = new Padding(0, 0, 0, 1)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var rot = Ui.Rotulo("Pasta das notas:", Ui.Fonte, Ui.Discreto);
            rot.Anchor = AnchorStyles.Left;
            t.Controls.Add(rot, 0, 0);

            _lblPasta.AutoSize = false;
            _lblPasta.Dock = DockStyle.Fill;
            _lblPasta.TextAlign = ContentAlignment.MiddleLeft;
            _lblPasta.AutoEllipsis = true;
            _lblPasta.Font = Ui.FonteNegrito;
            _lblPasta.ForeColor = Ui.Texto;
            t.Controls.Add(_lblPasta, 1, 0);

            var atualizar = Ui.Botao("Atualizar lista", false);
            atualizar.Click += delegate { Recarregar(null); };
            t.Controls.Add(atualizar, 2, 0);

            var config = Ui.Botao("Configurações", false);
            config.Click += delegate { AbrirConfig(false); };
            t.Controls.Add(config, 3, 0);
            return t;
        }

        private Control MontarLista()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _txtBusca.Dock = DockStyle.Fill;
            _txtBusca.Margin = new Padding(3, 3, 10, 8);
            Dica(_txtBusca, "Buscar pelo número da nota, chave ou nome do arquivo");
            _txtBusca.TextChanged += delegate { PreencherLista(_atual != null ? _atual.Caminho : null); };
            t.Controls.Add(_txtBusca, 0, 0);

            _chkSoCorrigir.Text = "Só as que precisam corrigir";
            _chkSoCorrigir.AutoSize = true;
            _chkSoCorrigir.Anchor = AnchorStyles.Left;
            _chkSoCorrigir.Margin = new Padding(3, 3, 3, 8);
            _chkSoCorrigir.CheckedChanged += delegate { PreencherLista(_atual != null ? _atual.Caminho : null); };
            t.Controls.Add(_chkSoCorrigir, 1, 0);

            _lista.Dock = DockStyle.Fill;
            _lista.View = View.Details;
            _lista.FullRowSelect = true;
            _lista.MultiSelect = false;
            _lista.HideSelection = false;
            _lista.GridLines = false;
            _lista.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _lista.BorderStyle = BorderStyle.FixedSingle;
            _lista.AllowDrop = true;
            _lista.Columns.Add("Situação", 150);
            _lista.Columns.Add("NF", 70, HorizontalAlignment.Right);
            _lista.Columns.Add("Emitente", 180);
            _lista.Columns.Add("Gerada em", 120);
            _lista.Columns.Add("Arquivo", 380);
            _lista.SelectedIndexChanged += delegate { AoSelecionar(); };
            _lista.DragEnter += AoArrastar;
            _lista.DragDrop += AoSoltar;
            t.Controls.Add(_lista, 0, 1);
            t.SetColumnSpan(_lista, 2);
            return t;
        }

        private Control MontarDetalhes()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16, 10, 14, 10) };
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var cab = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            _lblNF.AutoSize = true; _lblNF.Font = Ui.FonteTitulo; _lblNF.ForeColor = Ui.Texto;
            _lblEmit.AutoSize = true; _lblEmit.Font = Ui.Fonte; _lblEmit.ForeColor = Ui.Texto;
            _lblChave.AutoSize = true; _lblChave.Font = Ui.FontePequena; _lblChave.ForeColor = Ui.Discreto;
            cab.Controls.Add(_lblNF);
            cab.Controls.Add(_lblEmit);
            cab.Controls.Add(_lblChave);
            t.Controls.Add(cab, 0, 0);

            _rtb.Dock = DockStyle.Fill;
            _rtb.ReadOnly = true;
            _rtb.BorderStyle = BorderStyle.None;
            _rtb.BackColor = Ui.Painel;
            _rtb.DetectUrls = false;
            _rtb.Margin = new Padding(3, 10, 3, 10);
            t.Controls.Add(_rtb, 0, 1);

            var botoes = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            _btnCorrigir = Ui.Botao("Corrigir nota", true);
            _btnCorrigir.Font = Ui.FonteBotaoGrande;
            _btnCorrigir.MinimumSize = new Size(190, 44);
            _btnCorrigir.FlatAppearance.BorderColor = Ui.Verde;
            _btnCorrigir.BackColor = Ui.Verde;
            _btnCorrigir.Click += delegate { Corrigir(); };

            _btnRestaurar = Ui.Botao("Restaurar original", false);
            _btnRestaurar.MinimumSize = new Size(150, 44);
            _btnRestaurar.Click += delegate { Restaurar(); };

            _btnBackup = Ui.Botao("Abrir pasta de backup", false);
            _btnBackup.MinimumSize = new Size(150, 44);
            _btnBackup.Click += delegate { AbrirBackup(); };

            botoes.Controls.Add(_btnCorrigir);
            botoes.Controls.Add(_btnRestaurar);
            botoes.Controls.Add(_btnBackup);
            t.Controls.Add(botoes, 0, 2);

            MostrarDetalhes(null);
            return t;
        }

        // =====================================================================
        // ciclo de vida
        // =====================================================================

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_cfg.Completa) AbrirConfig(true);
            else AplicarConfig();
        }

        private void AbrirConfig(bool primeiraVez)
        {
            using (var f = new FormConfig(_cfg, primeiraVez))
            {
                if (f.ShowDialog(this) == DialogResult.OK) AplicarConfig();
                else if (primeiraVez) AplicarConfig(); // mostra o aviso de "configure"
            }
        }

        private void AplicarConfig()
        {
            _lblPasta.Text = _cfg.Completa ? _cfg.PastaNotas : "(não configurada - clique em Configurações)";
            _lblPasta.ForeColor = _cfg.Completa ? Ui.Texto : Ui.Vermelho;
            Vigiar();
            _cache.Clear();
            Recarregar(_extras.Count > 0 ? _extras[0] : null);
        }

        /// <summary>
        /// Acompanha a pasta: quando o sistema grava uma nota nova, ela aparece sozinha na lista.
        /// </summary>
        private void Vigiar()
        {
            if (_vigia != null) { _vigia.EnableRaisingEvents = false; _vigia.Dispose(); _vigia = null; }
            if (!_cfg.Completa) return;
            try
            {
                _vigia = new FileSystemWatcher(_cfg.PastaNotas, "*.xml")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    SynchronizingObject = this
                };
                FileSystemEventHandler h = delegate { _espera.Stop(); _espera.Start(); };
                _vigia.Created += h;
                _vigia.Changed += h;
                _vigia.Deleted += h;
                _vigia.Renamed += delegate { _espera.Stop(); _espera.Start(); };
                _vigia.EnableRaisingEvents = true;
            }
            catch { _vigia = null; /* pasta de rede sem suporte: fica no botao Atualizar */ }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_vigia != null) _vigia.Dispose();
            _espera.Dispose();
            base.OnFormClosed(e);
        }

        // =====================================================================
        // lista
        // =====================================================================

        private async void Recarregar(string selecionar)
        {
            int geracao = ++_geracao;
            if (selecionar == null && _atual != null) selecionar = _atual.Caminho;

            _status.Text = "Analisando notas...";
            Cursor = Cursors.AppStarting;

            string pasta = _cfg.Completa ? _cfg.PastaNotas : null;
            string indPres = _cfg.IndPres;
            int max = _cfg.MaxNotas;
            var extras = _extras.Where(File.Exists).ToList();
            var cacheAnterior = _cache;

            List<Analise> res;
            string falha = null;
            try
            {
                res = await Task.Run(() =>
                {
                    var arquivos = extras.Concat(Servico.ListarXmls(pasta, max))
                                         .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var lista = new List<Analise>();
                    foreach (string arq in arquivos)
                    {
                        // Reaproveita a analise se o arquivo nao mudou desde a ultima vez.
                        Analise a;
                        DateTime mod;
                        try { mod = File.GetLastWriteTime(arq); } catch { continue; }
                        if (!cacheAnterior.TryGetValue(arq, out a) || a.Modificado != mod)
                            a = Servico.Analisar(arq, indPres);
                        lista.Add(a);
                    }
                    return lista;
                });
            }
            catch (Exception ex)
            {
                res = new List<Analise>();
                falha = ex.Message;
            }

            if (geracao != _geracao || IsDisposed) return; // chegou uma recarga mais nova

            _analises = res;
            _cache = res.ToDictionary(a => a.Caminho, StringComparer.OrdinalIgnoreCase);
            Cursor = Cursors.Default;
            PreencherLista(selecionar);

            if (falha != null) _status.Text = "Não consegui ler a pasta das notas: " + falha;
            else if (!_cfg.Completa && extras.Count == 0) _status.Text = "Configure a pasta das notas para começar.";
            else
            {
                int precisa = res.Count(a => a.Situacao == Situacao.PrecisaCorrigir);
                _status.Text = string.Format("{0} nota(s) na lista  ·  {1} precisando de correção  ·  atualizado às {2:HH:mm:ss}{3}",
                    res.Count, precisa, DateTime.Now,
                    res.Count >= max ? "  ·  mostrando só as " + max + " mais recentes" : "");
            }
        }

        private void PreencherLista(string selecionar)
        {
            string busca = _txtBusca.Text.Trim();
            IEnumerable<Analise> vis = _analises;
            if (_chkSoCorrigir.Checked) vis = vis.Where(a => a.Situacao == Situacao.PrecisaCorrigir);
            if (busca.Length > 0)
                vis = vis.Where(a => Contem(a.Nome, busca)
                                  || (a.R != null && (Contem(a.R.NumeroNF, busca) || Contem(a.R.Chave, busca))));

            _lista.BeginUpdate();
            _lista.Items.Clear();
            ListViewItem alvo = null;
            foreach (Analise a in vis)
            {
                var it = new ListViewItem(a.TextoSituacao);
                it.SubItems.Add(a.R != null && !string.IsNullOrEmpty(a.R.NumeroNF) ? a.R.NumeroNF : "");
                it.SubItems.Add(a.R != null ? a.R.Emitente ?? "" : "");
                it.SubItems.Add(a.Modificado == DateTime.MinValue ? "" : a.Modificado.ToString("dd/MM/yyyy HH:mm"));
                it.SubItems.Add(a.Nome);
                it.Tag = a;
                it.UseItemStyleForSubItems = false;
                Color fundo = Ui.FundoSituacao(a.Situacao);
                foreach (ListViewItem.ListViewSubItem s in it.SubItems)
                {
                    s.BackColor = fundo;
                    s.ForeColor = Ui.Texto;
                }
                it.SubItems[0].ForeColor = Ui.CorSituacao(a.Situacao);
                it.SubItems[0].Font = Ui.FonteNegrito;
                _lista.Items.Add(it);
                if (selecionar != null && string.Equals(a.Caminho, selecionar, StringComparison.OrdinalIgnoreCase))
                    alvo = it;
            }
            _lista.EndUpdate();

            if (alvo != null)
            {
                alvo.Selected = true;
                alvo.Focused = true;
                alvo.EnsureVisible();
            }
            else if (_lista.Items.Count > 0)
            {
                // Sem selecao anterior: abre na primeira que precisa corrigir (ou na mais recente).
                ListViewItem primeira = _lista.Items.Cast<ListViewItem>()
                    .FirstOrDefault(i => ((Analise)i.Tag).Situacao == Situacao.PrecisaCorrigir) ?? _lista.Items[0];
                primeira.Selected = true;
                primeira.EnsureVisible();
            }
            else
            {
                MostrarDetalhes(null);
            }
        }

        private static bool Contem(string s, string busca)
        {
            return !string.IsNullOrEmpty(s) && s.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AoSelecionar()
        {
            if (_lista.SelectedItems.Count == 0) return; // trocando a selecao: espera o proximo evento
            MostrarDetalhes((Analise)_lista.SelectedItems[0].Tag);
        }

        // =====================================================================
        // detalhes
        // =====================================================================

        private void MostrarDetalhes(Analise a)
        {
            _atual = a;
            _rtb.Clear();

            if (a == null)
            {
                _lblNF.Text = "Nenhuma nota selecionada";
                _lblEmit.Text = "";
                _lblChave.Text = "";
                if (!_cfg.Completa)
                    Escrever("Clique em Configurações e informe a pasta onde o sistema grava os XMLs das notas.", Estilo.Normal);
                else if (_analises.Count == 0)
                    Escrever("Nenhum XML encontrado na pasta das notas. Quando o sistema gerar uma nota, ela aparece "
                           + "aqui sozinha. Você também pode arrastar um arquivo XML para esta janela.", Estilo.Normal);
                else
                    Escrever("Selecione uma nota na lista. As notas em vermelho precisam de correção.", Estilo.Normal);
                AtualizarBotoes();
                return;
            }

            Resultado r = a.R;
            bool temDados = r != null && r.Ok;
            _lblNF.Text = temDados && !string.IsNullOrEmpty(r.NumeroNF) ? "Nota " + r.NumeroNF : a.Nome;
            _lblEmit.Text = temDados ? (r.Emitente ?? "") + "  ·  " + r.QtdItens + " ite" + (r.QtdItens == 1 ? "m" : "ns") : "";
            _lblChave.Text = temDados && !string.IsNullOrEmpty(r.Chave) ? "Chave " + r.Chave : a.Caminho;

            foreach (Trecho t in Relatorio.Montar(a)) Escrever(t.Texto, t.Estilo);
            _rtb.SelectionStart = 0;
            _rtb.ScrollToCaret();
            AtualizarBotoes();
        }

        private void Escrever(string texto, Estilo e)
        {
            Font f = Ui.Fonte;
            Color c = Ui.Texto;
            int recuo = 0;
            string antes = "";
            switch (e)
            {
                case Estilo.Titulo: f = Ui.FonteSubtitulo; c = Ui.Vermelho; break;
                case Estilo.Ok: f = Ui.FonteSubtitulo; c = Ui.Verde; break;
                case Estilo.Subtitulo: f = Ui.FonteNegrito; c = Ui.Texto; antes = "\n"; break;
                case Estilo.Item: f = Ui.Fonte; c = Ui.Texto; recuo = 12; break;
                case Estilo.Alerta: f = Ui.FonteNegrito; c = Ui.Ambar; break;
                case Estilo.Discreto: f = Ui.FontePequena; c = Ui.Discreto; break;
            }
            _rtb.SelectionStart = _rtb.TextLength;
            _rtb.SelectionLength = 0;
            if (antes.Length > 0) _rtb.SelectedText = antes;
            _rtb.SelectionFont = f;
            _rtb.SelectionColor = c;
            _rtb.SelectionIndent = recuo;
            _rtb.SelectionHangingIndent = recuo > 0 ? 14 : 0;
            _rtb.SelectedText = texto + "\n";
            if (e == Estilo.Titulo || e == Estilo.Ok || e == Estilo.Normal || e == Estilo.Alerta)
                _rtb.SelectedText = "\n";
        }

        private void AtualizarBotoes()
        {
            Analise a = _atual;
            bool podeCorrigir = a != null && a.Situacao == Situacao.PrecisaCorrigir;
            _btnCorrigir.Enabled = podeCorrigir;
            _btnCorrigir.BackColor = podeCorrigir ? Ui.Verde : Ui.Borda;
            _btnCorrigir.FlatAppearance.BorderColor = podeCorrigir ? Ui.Verde : Ui.Borda;
            _btnCorrigir.ForeColor = podeCorrigir ? Color.White : Ui.Discreto;

            _btnRestaurar.Enabled = a != null && Servico.UltimoBackup(a.Caminho, _cfg.PastaBackup) != null;
            _btnBackup.Enabled = !string.IsNullOrEmpty(_cfg.PastaBackup);
        }

        // =====================================================================
        // acoes
        // =====================================================================

        private void Corrigir()
        {
            Analise a = _atual;
            if (a == null) return;
            if (string.IsNullOrEmpty(_cfg.PastaBackup))
            {
                MessageBox.Show(this, "Configure a pasta de backup antes de corrigir.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                AbrirConfig(false);
                return;
            }

            Retorno ret;
            Cursor = Cursors.WaitCursor;
            try { ret = Servico.Corrigir(a.Caminho, _cfg); }
            finally { Cursor = Cursors.Default; }

            string nf = a.R != null && !string.IsNullOrEmpty(a.R.NumeroNF) ? "A nota " + a.R.NumeroNF : "A nota";
            if (ret.Sucesso && ret.Backup != null)
            {
                string msg = nf + " foi corrigida.\n\nAgora volte ao sistema e transmita a nota novamente.";
                if (a.R != null && a.R.TinhaAssinatura)
                    msg += "\n\nAtenção: o XML estava assinado. O sistema precisa assinar de novo ao transmitir.";
                if (ret.Depois != null && ret.Depois.Situacao == Situacao.ComObservacoes)
                    msg += "\n\nHá observações para o contador conferir (não impedem a transmissão).";
                msg += "\n\nO original foi guardado em:\n" + ret.Backup;
                MessageBox.Show(this, msg, "Nota corrigida", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, ret.Mensagem, ret.Sucesso ? Text : "Não foi possível corrigir",
                    MessageBoxButtons.OK, ret.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            Recarregar(a.Caminho);
        }

        private void Restaurar()
        {
            Analise a = _atual;
            if (a == null) return;
            string b = Servico.UltimoBackup(a.Caminho, _cfg.PastaBackup);
            if (b == null) { AtualizarBotoes(); return; }

            if (MessageBox.Show(this,
                    "Voltar a nota para como ela estava antes da correção?\n\nBackup usado:\n" + b,
                    "Restaurar original", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            Retorno ret = Servico.Restaurar(a.Caminho, _cfg);
            if (!ret.Sucesso)
                MessageBox.Show(this, ret.Mensagem, "Restaurar original", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                _status.Text = "Original restaurado a partir de " + Path.GetFileName(b);
            Recarregar(a.Caminho);
        }

        private void AbrirBackup()
        {
            try
            {
                Directory.CreateDirectory(_cfg.PastaBackup);
                string b = _atual != null ? Servico.UltimoBackup(_atual.Caminho, _cfg.PastaBackup) : null;
                if (b != null) Process.Start("explorer.exe", "/select,\"" + b + "\"");
                else Process.Start("explorer.exe", "\"" + _cfg.PastaBackup + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não consegui abrir a pasta:\n" + ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // =====================================================================
        // arrastar e soltar
        // =====================================================================

        private static string[] XmlsArrastados(DragEventArgs e)
        {
            var arq = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (arq == null) return new string[0];
            return arq.Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && File.Exists(f)).ToArray();
        }

        private void AoArrastar(object sender, DragEventArgs e)
        {
            e.Effect = XmlsArrastados(e).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void AoSoltar(object sender, DragEventArgs e)
        {
            string[] arq = XmlsArrastados(e);
            if (arq.Length == 0) return;
            foreach (string f in arq)
            {
                string full = Path.GetFullPath(f);
                if (!_extras.Contains(full, StringComparer.OrdinalIgnoreCase)) _extras.Insert(0, full);
            }
            _txtBusca.Text = "";
            _chkSoCorrigir.Checked = false;
            Recarregar(Path.GetFullPath(arq[0]));
        }

        // =====================================================================
        // apoio
        // =====================================================================

        /// <summary>Texto de dica dentro da caixa de busca (EM_SETCUEBANNER), so no Windows.</summary>
        private static void Dica(TextBox t, string texto)
        {
            t.HandleCreated += delegate
            {
                try { NativeMethods.SendMessage(t.Handle, 0x1501, (IntPtr)1, texto); } catch { }
            };
        }
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }
}
