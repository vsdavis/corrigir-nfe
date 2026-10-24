using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CorrigirNFe
{
    /// <summary>
    /// Configuracao da maquina: onde ficam os XMLs e onde guardar os backups.
    /// Gravada em %LocalAppData%\CorrigirNFe\config.ini (sempre gravavel, mesmo que o
    /// .exe esteja numa pasta protegida).
    /// </summary>
    internal sealed class Config
    {
        public string PastaNotas = "";
        public string PastaBackup = "";
        public string IndPres = "9";

        /// <summary>Quantas notas (as mais recentes) mostrar na lista.</summary>
        public int MaxNotas = 300;

        public static string Arquivo
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CorrigirNFe");
                return Path.Combine(dir, "config.ini");
            }
        }

        public bool Completa
        {
            get
            {
                return !string.IsNullOrWhiteSpace(PastaNotas) && Directory.Exists(PastaNotas)
                    && !string.IsNullOrWhiteSpace(PastaBackup);
            }
        }

        public static Config Carregar()
        {
            var c = new Config();
            try
            {
                if (!File.Exists(Arquivo)) return c;
                foreach (string linha in File.ReadAllLines(Arquivo, Encoding.UTF8))
                {
                    int eq = linha.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = linha.Substring(0, eq).Trim();
                    string v = linha.Substring(eq + 1).Trim();
                    if (k == "PastaNotas") c.PastaNotas = v;
                    else if (k == "PastaBackup") c.PastaBackup = v;
                    else if (k == "IndPres" && v.Length == 1) c.IndPres = v;
                    else if (k == "MaxNotas") { int n; if (int.TryParse(v, out n) && n > 0) c.MaxNotas = n; }
                }
            }
            catch { /* config corrompida: comeca do zero */ }
            return c;
        }

        public void Salvar()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Arquivo));
            var linhas = new List<string>
            {
                "PastaNotas=" + PastaNotas,
                "PastaBackup=" + PastaBackup,
                "IndPres=" + IndPres,
                "MaxNotas=" + MaxNotas
            };
            File.WriteAllLines(Arquivo, linhas, new UTF8Encoding(false));
        }

        /// <summary>
        /// Valida as duas pastas antes de salvar. Devolve null se estiver tudo certo,
        /// ou a mensagem para mostrar ao usuario.
        /// </summary>
        public static string Validar(string notas, string backup)
        {
            if (string.IsNullOrWhiteSpace(notas)) return "Informe a pasta onde ficam os XMLs das notas.";
            if (!Directory.Exists(notas)) return "A pasta das notas nao existe:\n" + notas;
            if (string.IsNullOrWhiteSpace(backup)) return "Informe a pasta de backup.";

            string n = Normalizar(notas), b = Normalizar(backup);
            if (n == b)
                return "A pasta de backup precisa ser diferente da pasta das notas.\n\n"
                     + "Se for a mesma, o sistema emissor pode pegar o backup no lugar da nota corrigida.";

            try { Directory.CreateDirectory(backup); }
            catch (Exception ex) { return "Nao consegui criar a pasta de backup:\n" + ex.Message; }

            string erro = TestarEscrita(backup);
            if (erro != null) return "Sem permissao para gravar na pasta de backup:\n" + erro;
            erro = TestarEscrita(notas);
            if (erro != null) return "Sem permissao para gravar na pasta das notas (necessario para corrigir):\n" + erro;
            return null;
        }

        private static string TestarEscrita(string pasta)
        {
            string teste = Path.Combine(pasta, ".corrigir-nfe-teste-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(teste, "ok");
                File.Delete(teste);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        internal static string Normalizar(string p)
        {
            return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       .ToUpperInvariant();
        }
    }
}
