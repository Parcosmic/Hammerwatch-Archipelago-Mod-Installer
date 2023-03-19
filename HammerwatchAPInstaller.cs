using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Documents;
using System.Windows.Forms;
using BsDiff.Core;
using Microsoft.Win32;

namespace Hammerpelago_Installer
{
    class HammerwatchAPInstaller
    {
        [STAThread]
        private static void Main(string[] args)
        {
            const string hammerwatchExeName = "Hammerwatch.exe";
            const string hammerpelagoName = "HammerwatchAP.exe";
            const string patchName = "HammerwatchAP.bsdiff";

            Console.WriteLine("Hammerwatch Archipelago Installer");

            string installerPath = Directory.GetCurrentDirectory();
            string hammerwatchPath = null;

            //Check if the installer is in the install directory, if it is skip the file selection dialogue
            if (!File.Exists(hammerwatchExeName))
            {
                string initialDirectory = "C:\\";
                try
                {
                    RegistryKey steamKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Wow6432Node\\Valve\\Steam");
                    string steamDir = steamKey.GetValue("InstallPath") as string;
                    StreamReader libraryFoldersReader = File.OpenText(Path.Combine(steamDir, "steamapps", "libraryfolders.vdf"));
                    List<string> paths = new List<string>();
                    //Parse through folders file and keep track of steam install paths
                    while (!libraryFoldersReader.EndOfStream)
                    {
                        string line = libraryFoldersReader.ReadLine();
                        if(line.Contains("\"path\""))
                        {
                            line = line.Replace("\"path\"", "");
                            line = line.Substring(line.IndexOf('"') + 1);
                            line = line.Remove(line.Length - 1);
                            paths.Add(line);
                        }
                    }
                    for(int p = 0; p < paths.Count; p++)
                    {
                        //For some reason games are installed in either "steam" or "steamapps"
                        //Not sure what the distinction is but seems like C:// is "steam" and D:// is "steamapps"?
                        string path1 = Path.Combine(paths[p], "steam\\common\\Hammerwatch");
                        if (Directory.Exists(path1))
                        {
                            initialDirectory = path1;
                            break;
                        }
                        string path2 = Path.Combine(paths[p], "steamapps\\common\\Hammerwatch");
                        if (Directory.Exists(path2))
                        {
                            initialDirectory = path2;
                            break;
                        }
                    }
                }
                catch(Exception e)
                {
                    Console.Error.WriteLine("An error has occured when trying to find the Hammerwatch install directory:");
                    Console.Error.WriteLine(e.ToString());
                }

                Console.WriteLine("Please select a file in the dialogue window");
                using System.Windows.Forms.OpenFileDialog openFileDialog = new();
                openFileDialog.InitialDirectory = initialDirectory;
                openFileDialog.Filter = "Hammerwatch.exe|Hammerwatch.exe";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    hammerwatchPath = Path.GetDirectoryName(openFileDialog.FileName);
                }
            }
            else
            {
                Console.Error.WriteLine("Installer located in Hammerwatch install directory");
                hammerwatchPath = installerPath;
            }

            if (hammerwatchPath != null)
            {
                if (hammerwatchPath != installerPath)
                {
                    //Copy mod files to Hammerwatch directory
                    DeepCopy(installerPath, hammerwatchPath, new List<string> { "LICENSE.txt", "HammerwatchAPInstaller.exe", "HammerwatchAP.bsdiff" });
                }
                try
                {
                    string hammerwatchExePath = Path.Combine(hammerwatchPath, hammerwatchExeName);
                    string hammerpelagoExePath = Path.Combine(hammerwatchPath, hammerpelagoName);
                    string vanillaLocation = Path.Combine(hammerwatchPath, "archipelago-assets", hammerwatchExeName);

                    //Open hammerwatch exe path and compute the hash to make sure that it's the right version/vanilla
                    FileStream hammerwatchExeStream = new(hammerwatchExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using MD5 md5 = MD5.Create();
                    string exeHashString = BitConverter.ToString(md5.ComputeHash(hammerwatchExeStream)).Replace("-", "").ToLower();
                    hammerwatchExeStream.Dispose();

                    switch(exeHashString)
                    {
                        case "ddf8414912a48b5b2b77873a66a41b57": //Vanilla hash
                            //Backup the vanilla exe so uninstalling/reinstalling is easier
                            File.Copy(hammerwatchExePath, vanillaLocation);
                            break;
                        default: //During dev the hash will be changing rapidly, so we'll keep this default block for now
                            //Replace the modded exe with the vanilla one to apply the bsdiff
                            Console.WriteLine("Mod already installed, removing and reinstalling");
                            File.Copy(vanillaLocation, hammerwatchExePath, true);
                            break;
                        //default:
                        //    Console.Error.WriteLine("Incorrect Hammerwatch version, make sure that version 1.41 is installed!");
                        //    Exit();
                        //    return;
                    }

                    //Reopen the hammerwatch exe (should be vanilla at this point) and apply the bsdiff
                    FileStream input = new(hammerwatchExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    FileStream output = new(hammerpelagoExePath, FileMode.Create);
                    BinaryPatchUtility.Apply(input, () => new FileStream(Path.Combine(installerPath, patchName), FileMode.Open, FileAccess.Read, FileShare.Read), output);
                    input.Dispose();
                    output.Dispose();
                    File.Move(hammerpelagoExePath, hammerwatchExePath, true);
                }
                catch (FileNotFoundException ex)
                {
                    Console.Error.WriteLine($"Could not open '{ex.FileName}'.");
                    Console.Error.WriteLine("Make sure all supplied mod files exist in the same directory as the installer");
                    Exit();
                    return;
                }
                Console.WriteLine("Patching successful!");
            }
            else
            {
                Console.WriteLine("No valid file selected, exiting installation process");
            }
            
            Exit();
        }

        private static void Exit()
        {
            Console.WriteLine("Press any key to close this window...");
            Console.Read();
        }

        private static void DeepCopy(string fromFolder, string toFolder, List<string> exceptionFiles = null)
        {
            string[] files = Directory.GetFiles(fromFolder);
            Directory.CreateDirectory(toFolder);
            foreach (string file in files)
            {
                if(exceptionFiles != null && exceptionFiles.Contains(Path.GetFileName(file))) continue;
                string dest = Path.Combine(toFolder, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
            string[] folders = Directory.GetDirectories(fromFolder);
            foreach (string folder in folders)
            {
                DeepCopy(folder, Path.Combine(toFolder, Path.GetFileNameWithoutExtension(folder)));
            }
        }
    }
}
