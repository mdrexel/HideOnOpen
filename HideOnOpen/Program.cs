
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HideOnOpen
{
    public class Program
    {
        private const string RuleFile = "Rules.txt";

        public static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                MessageBox.Show(
                    $"An unexpected number of start arguments were supplied. Expected: 1, Actual: {args.Length}",
                    "HideOnOpen failed to complete.",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            string file = args[0];

            try
            {
                string raw = GetCommandLine();

                using Process self = Process.GetCurrentProcess();
                string ownDirectory = Path.GetDirectoryName(self.MainModule.FileName);
                string ruleLocation = Path.Combine(ownDirectory, RuleFile);

                Dictionary<string, string> applications =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] rules = File.ReadAllLines(ruleLocation);
                foreach (string rule in rules)
                {
                    string[] parts = rule.Split(' ');
                    applications[parts[0]] = string.Join(" ", parts.Skip(1));
                }

                string extension = Path.GetExtension(file);
                if (!applications.TryGetValue(extension, out string applicationFullPath))
                {
                    throw new InvalidOperationException($"Missing extension: {extension}");
                }

                using Process process = Process.Start(applicationFullPath, raw);

                File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.Hidden);
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    $"{e.GetType().FullName}: {e.Message}\r\n\r\n{e.StackTrace}",
                    "HideOnOpen failed to complete.",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // https://stackoverflow.com/questions/36204461/how-to-remove-the-exe-part-of-the-command-line/66242266#66242266
        private static string GetCommandLine()
        {
            // Separate the args from the exe path.. incl handling of dquote-delimited full/relative paths.
            Regex fullCommandLinePattern = new Regex(@"
                ^ #anchor match to start of string
                    (?<exe> #capture the executable name; can be dquote-delimited or not
                        (\x22[^\x22]+\x22) #case: dquote-delimited
                        | #or
                        ([^\s]+) #case: no dquotes
                    )
                    \s* #chomp zero or more whitespace chars, after <exe>
                    (?<args>.*) #capture the remainder of the command line
                $ #match all the way to end of string
                ",
                RegexOptions.IgnorePatternWhitespace|
                RegexOptions.ExplicitCapture|
                RegexOptions.CultureInvariant
            );

            Match m = fullCommandLinePattern.Match(Marshal.PtrToStringAuto(PInvoke.GetCommandLine()));
            if (!m.Success)
            {
                throw new ApplicationException("Failed to extract command line.");
            }

            // Note: will return empty-string if no args after exe name.
            string commandLineArgs = m.Groups["args"].Value;
            return commandLineArgs;
        }
    }

    internal static class PInvoke
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetCommandLine();
    }
}
