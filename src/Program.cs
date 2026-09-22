using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace LimpezaB
{
    internal static class Program
    {
        private const string Versao = "1.0.0";
        private const long UmGB = 1024L * 1024L * 1024L;
        private static readonly string BaseDados = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LimpezaB");
        private static readonly string LogPath = Path.Combine(BaseDados, "limpezaB.log");

        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Directory.CreateDirectory(BaseDados);

            if (args.Any(a => a.Equals("--automatico", StringComparison.OrdinalIgnoreCase)))
            {
                ExecutarManutencao(true);
                return;
            }

            while (true)
            {
                try { Console.Clear(); } catch { }
                Cabecalho();
                Console.WriteLine("  1. Analisar o computador");
                Console.WriteLine("  2. Limpar arquivos temporários antigos");
                Console.WriteLine("  3. Localizar arquivos grandes");
                Console.WriteLine("  4. Analisar e encerrar processos");
                Console.WriteLine("  5. Executar agora");
                Console.WriteLine("  6. Configurar execução automática");
                Console.WriteLine("  7. Remover execução automática");
                Console.WriteLine("  8. Ver histórico");
                Console.WriteLine("  0. Sair");
                Console.Write("\nEscolha uma opção: ");
                string escolha = Console.ReadLine();

                try
                {
                    switch (escolha)
                    {
                        case "1": Analisar(); break;
                        case "2": LimparTemporarios(false); break;
                        case "3": ListarArquivosGrandes(); break;
                        case "4": GerenciarProcessos(); break;
                        case "5": ExecutarManutencao(false); break;
                        case "6": ConfigurarAgendamento(); break;
                        case "7": RemoverAgendamento(); break;
                        case "8": MostrarHistorico(); break;
                        case "0": return;
                        default: Console.WriteLine("Opção inválida."); break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\nNão foi possível concluir: " + ex.Message);
                    Registrar("ERRO: " + ex);
                }

                Console.WriteLine("\nPressione Enter para voltar ao menu.");
                Console.ReadLine();
            }
        }

        private static void Cabecalho()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("========================================================");
            Console.WriteLine(" limpezaB.exe — manutenção do computador  v" + Versao);
            Console.WriteLine("========================================================");
            Console.ResetColor();
            Console.WriteLine("Ações destrutivas exibem os alvos antes da confirmação.\n");
        }

        private static void Analisar()
        {
            Console.WriteLine("\nAnalisando...");
            DriveInfo disco = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            Console.WriteLine("Disco {0}: {1:N1} GB livres de {2:N1} GB",
                disco.Name, disco.AvailableFreeSpace / (double)UmGB, disco.TotalSize / (double)UmGB);

            List<FileInfo> antigos = ObterTemporariosAntigos(14);
            long bytes = antigos.Sum(f => TamanhoSeguro(f));
            Console.WriteLine("Temporários com mais de 14 dias: {0} ({1})", antigos.Count, FormatarBytes(bytes));

            Process[] maiores = Process.GetProcesses().OrderByDescending(p => MemoriaSegura(p)).Take(8).ToArray();
            Console.WriteLine("\nMaiores consumidores de memória:");
            foreach (Process p in maiores)
                Console.WriteLine("  PID {0,-7} {1,-28} {2,10}", p.Id, p.ProcessName, FormatarBytes(MemoriaSegura(p)));
        }

        private static long LimparTemporarios(bool automatico)
        {
            List<FileInfo> arquivos = ObterTemporariosAntigos(14);
            long total = arquivos.Sum(f => TamanhoSeguro(f));
            Console.WriteLine("\nForam encontrados {0} arquivos temporários antigos ({1}).", arquivos.Count, FormatarBytes(total));
            if (arquivos.Count == 0) return 0;

            foreach (FileInfo f in arquivos.Take(15)) Console.WriteLine("  " + f.FullName);
            if (arquivos.Count > 15) Console.WriteLine("  ... e mais " + (arquivos.Count - 15));

            if (!automatico && !Confirmar("Excluir esses arquivos temporários?")) return 0;

            long removidos = 0;
            int quantidade = 0;
            foreach (FileInfo f in arquivos)
            {
                try
                {
                    if (!CaminhoDentro(f.FullName, Path.GetTempPath()) || EhLink(f)) continue;
                    long tamanho = TamanhoSeguro(f);
                    using (FileStream teste = new FileStream(f.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    f.Delete();
                    if (!f.Exists) { removidos += tamanho; quantidade++; }
                }
                catch { }
            }
            Console.WriteLine("Removidos: {0} arquivos, {1}.", quantidade, FormatarBytes(removidos));
            Registrar("Limpeza de temporários: " + quantidade + " arquivos; " + removidos + " bytes.");
            return removidos;
        }

        private static List<FileInfo> ObterTemporariosAntigos(int dias)
        {
            DateTime limite = DateTime.Now.AddDays(-dias);
            return EnumerarArquivosSeguro(Path.GetTempPath())
                .Where(f => f.LastWriteTime < limite && !EhLink(f)).ToList();
        }

        private static void ListarArquivosGrandes()
        {
            Console.Write("\nTamanho mínimo em GB [1]: ");
            string entrada = Console.ReadLine();
            double minimoGb;
            if (!double.TryParse(entrada, NumberStyles.Float, CultureInfo.CurrentCulture, out minimoGb)) minimoGb = 1;
            long minimo = (long)(Math.Max(0.1, minimoGb) * UmGB);

            string perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] pastas = { "Desktop", "Documents", "Downloads", "Pictures", "Videos", "Music" };
            var arquivos = new List<FileInfo>();
            Console.WriteLine("Procurando por metadados; arquivos de nuvem offline serão ignorados...");
            foreach (string pasta in pastas)
            {
                string caminho = Path.Combine(perfil, pasta);
                arquivos.AddRange(EnumerarArquivosSeguro(caminho).Where(f => TamanhoSeguro(f) >= minimo));
            }

            arquivos = arquivos.OrderByDescending(f => TamanhoSeguro(f)).ToList();
            if (arquivos.Count == 0) { Console.WriteLine("Nenhum arquivo encontrado."); return; }
            foreach (FileInfo f in arquivos.Take(100))
                Console.WriteLine("  {0,10}  {1}", FormatarBytes(TamanhoSeguro(f)), f.FullName);
            Console.WriteLine("\nNenhum arquivo pessoal é excluído automaticamente.");
        }

        private static void GerenciarProcessos()
        {
            Console.WriteLine("\nProcessos do usuário com maior uso de memória:");
            string usuario = WindowsIdentity.GetCurrent().Name;
            var processos = Process.GetProcesses()
                .Where(p => p.Id != Process.GetCurrentProcess().Id)
                .OrderByDescending(p => MemoriaSegura(p)).Take(30).ToList();

            foreach (Process p in processos)
            {
                string janela = TituloSeguro(p);
                Console.WriteLine("  PID {0,-7} {1,-28} {2,10}  {3}", p.Id, p.ProcessName,
                    FormatarBytes(MemoriaSegura(p)), string.IsNullOrWhiteSpace(janela) ? "(sem janela)" : janela);
            }

            Console.Write("\nDigite o PID para solicitar encerramento, ou Enter para cancelar: ");
            int pid;
            if (!int.TryParse(Console.ReadLine(), out pid)) return;
            Process alvo;
            try { alvo = Process.GetProcessById(pid); }
            catch { Console.WriteLine("Processo não encontrado."); return; }

            if (Protegido(alvo.ProcessName))
            {
                Console.WriteLine("Esse processo é protegido pelo limpezaB e não será encerrado.");
                return;
            }
            Console.WriteLine("Alvo: {0} (PID {1})", alvo.ProcessName, alvo.Id);
            if (!Confirmar("Tentar fechar esse processo de forma normal? Trabalho não salvo pode ser perdido.")) return;

            bool pediu = false;
            try { pediu = alvo.CloseMainWindow(); } catch { }
            if (!pediu)
            {
                Console.WriteLine("O processo não aceita encerramento normal. Ele não foi forçado.");
                return;
            }
            try { alvo.WaitForExit(5000); } catch { }
            Console.WriteLine(alvo.HasExited ? "Processo encerrado." : "O processo permaneceu aberto; nenhum encerramento forçado foi feito.");
            if (alvo.HasExited) Registrar("Processo encerrado normalmente: " + alvo.ProcessName + " PID " + pid);
        }

        private static void ExecutarManutencao(bool automatico)
        {
            Console.WriteLine("\nIniciando manutenção " + (automatico ? "automática" : "completa") + "...");
            DriveInfo disco = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            long antes = disco.AvailableFreeSpace;
            long removidos = LimparTemporarios(automatico);
            disco = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            long depois = disco.AvailableFreeSpace;
            Console.WriteLine("Espaço livre: {0} → {1}", FormatarBytes(antes), FormatarBytes(depois));
            Console.WriteLine("Processos não são encerrados automaticamente sem prova de que são descartáveis.");
            Registrar("Manutenção concluída. Bytes removidos=" + removidos + "; livre antes=" + antes + "; depois=" + depois);
        }

        private static void ConfigurarAgendamento()
        {
            Console.WriteLine("\nFrequência: 1=Diária, 2=Semanal, 3=Mensal");
            Console.Write("Escolha [2]: ");
            string escolha = Console.ReadLine();
            string agenda = escolha == "1" ? "DAILY" : escolha == "3" ? "MONTHLY" : "WEEKLY";
            Console.Write("Horário (HH:mm) [10:00]: ");
            string hora = Console.ReadLine();
            TimeSpan horario;
            if (!TimeSpan.TryParse(hora, out horario)) horario = new TimeSpan(10, 0, 0);

            string exe = Process.GetCurrentProcess().MainModule.FileName;
            string argumentos = "/Create /F /TN \"LimpezaB-Manutencao\" /SC " + agenda +
                " /ST " + horario.ToString(@"hh\:mm") + " /TR \"\\\"" + exe + "\\\" --automatico\"";
            ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", argumentos);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (Process p = Process.Start(psi))
            {
                string saida = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit();
                Console.WriteLine(saida.Trim());
                if (p.ExitCode != 0) throw new InvalidOperationException("O Windows não criou a tarefa agendada.");
            }
            Registrar("Agendamento configurado: " + agenda + " às " + horario.ToString(@"hh\:mm"));
        }

        private static void RemoverAgendamento()
        {
            if (!Confirmar("Remover a tarefa automática LimpezaB-Manutencao?")) return;
            Process.Start(new ProcessStartInfo("schtasks.exe", "/Delete /F /TN \"LimpezaB-Manutencao\"")
            { UseShellExecute = false }).WaitForExit();
            Console.WriteLine("Solicitação concluída.");
            Registrar("Agendamento removido.");
        }

        private static void MostrarHistorico()
        {
            Console.WriteLine("\nArquivo: " + LogPath + "\n");
            if (!File.Exists(LogPath)) { Console.WriteLine("Ainda não há histórico."); return; }
            foreach (string linha in File.ReadLines(LogPath).Reverse().Take(50).Reverse()) Console.WriteLine(linha);
        }

        private static IEnumerable<FileInfo> EnumerarArquivosSeguro(string raiz)
        {
            if (!Directory.Exists(raiz)) yield break;
            var pilha = new Stack<string>();
            pilha.Push(Path.GetFullPath(raiz));
            while (pilha.Count > 0)
            {
                string atual = pilha.Pop();
                string[] arquivos = new string[0], diretorios = new string[0];
                try { arquivos = Directory.GetFiles(atual); } catch { }
                foreach (string arquivo in arquivos)
                {
                    FileInfo info = null;
                    try
                    {
                        info = new FileInfo(arquivo);
                        if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0) info = null;
                    }
                    catch { }
                    if (info != null) yield return info;
                }
                try { diretorios = Directory.GetDirectories(atual); } catch { }
                foreach (string diretorio in diretorios)
                {
                    try
                    {
                        var info = new DirectoryInfo(diretorio);
                        if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) == 0) pilha.Push(diretorio);
                    }
                    catch { }
                }
            }
        }

        private static bool CaminhoDentro(string caminho, string raiz)
        {
            string alvo = Path.GetFullPath(caminho);
            string basePath = Path.GetFullPath(raiz).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return alvo.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EhLink(FileSystemInfo f)
        {
            try { return (f.Attributes & FileAttributes.ReparsePoint) != 0; } catch { return true; }
        }
        private static long TamanhoSeguro(FileInfo f) { try { return f.Length; } catch { return 0; } }
        private static long MemoriaSegura(Process p) { try { return p.WorkingSet64; } catch { return 0; } }
        private static string TituloSeguro(Process p) { try { return p.MainWindowTitle; } catch { return ""; } }
        private static bool Protegido(string nome)
        {
            string n = nome.ToLowerInvariant();
            string[] protegidos = { "system", "idle", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost", "explorer", "dwm", "fontdrvhost", "memory compression", "msmpeng", "limpezab" };
            return protegidos.Any(x => n == x);
        }
        private static bool Confirmar(string pergunta)
        {
            Console.Write(pergunta + " [s/N]: ");
            return string.Equals(Console.ReadLine(), "s", StringComparison.OrdinalIgnoreCase);
        }
        private static string FormatarBytes(long bytes)
        {
            string[] unidades = { "B", "KB", "MB", "GB", "TB" };
            double valor = Math.Max(0, bytes); int i = 0;
            while (valor >= 1024 && i < unidades.Length - 1) { valor /= 1024; i++; }
            return valor.ToString("N1") + " " + unidades[i];
        }
        private static void Registrar(string mensagem)
        {
            try { File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + mensagem + Environment.NewLine, Encoding.UTF8); } catch { }
        }
    }
}
