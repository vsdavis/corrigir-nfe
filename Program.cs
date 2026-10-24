using System;
using System.Threading;
using System.Windows.Forms;

namespace CorrigirNFe
{
    internal static class Program
    {
        internal const string Versao = "2.0";

        /// <summary>
        /// Abre a tela. Se um XML for arrastado sobre o .exe (ou passado como argumento),
        /// a tela ja abre com ele selecionado.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
#if !MONO
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { Falha(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Falha(e.ExceptionObject as Exception);
            };

            string arquivo = args.Length > 0 ? args[0] : null;
            Application.Run(new FormPrincipal(Config.Carregar(), arquivo));
        }

        private static void Falha(Exception ex)
        {
            MessageBox.Show(
                "Aconteceu um erro inesperado. Nenhuma nota foi alterada sem backup.\n\n"
              + "Se repetir, envie esta mensagem para o suporte:\n\n"
              + (ex != null ? ex.GetType().Name + ": " + ex.Message : "(sem detalhes)"),
                "Corrigir NF-e", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
