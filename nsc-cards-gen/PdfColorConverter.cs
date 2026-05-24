using System.Diagnostics;

namespace SvgPdfGenerator;

public sealed class PdfColorConverter
{
    private static readonly string[] GhostscriptExecutableCandidates =
    [
        "gswin64c.exe",
        "gswin32c.exe",
        "gs"
    ];

    public PdfColorConversionResult ConvertToCmyk(
        string inputPdfPath,
        string outputPdfPath,
        string iccProfilePath,
        string? ghostscriptExecutablePath = null)
    {
        if (string.IsNullOrWhiteSpace(inputPdfPath))
        {
            throw new ArgumentException("Input PDF path must not be empty.", nameof(inputPdfPath));
        }

        if (string.IsNullOrWhiteSpace(outputPdfPath))
        {
            throw new ArgumentException("Output PDF path must not be empty.", nameof(outputPdfPath));
        }

        if (string.IsNullOrWhiteSpace(iccProfilePath))
        {
            throw new ArgumentException("ICC profile path must not be empty.", nameof(iccProfilePath));
        }

        string fullInputPdfPath = Path.GetFullPath(inputPdfPath);
        string fullOutputPdfPath = Path.GetFullPath(outputPdfPath);
        string fullIccProfilePath = Path.GetFullPath(iccProfilePath);

        if (!File.Exists(fullInputPdfPath))
        {
            return PdfColorConversionResult.Skipped($"Eingabe-PDF nicht gefunden: {fullInputPdfPath}");
        }

        if (!File.Exists(fullIccProfilePath))
        {
            return PdfColorConversionResult.Skipped($"ICC-Profil nicht gefunden: {fullIccProfilePath}");
        }

        string? ghostscriptExecutable = ResolveGhostscriptExecutable(ghostscriptExecutablePath);
        if (ghostscriptExecutable == null)
        {
            return PdfColorConversionResult.Skipped(
                "Ghostscript wurde nicht gefunden. Erwartet wird gswin64c.exe, gswin32c.exe oder gs im PATH.");
        }

        string? outputDirectory = Path.GetDirectoryName(fullOutputPdfPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ghostscriptExecutable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        AddGhostscriptArguments(startInfo, fullInputPdfPath, fullOutputPdfPath, fullIccProfilePath);

        Console.WriteLine($"Starting Ghostscript to convert '{Path.GetFileName(fullInputPdfPath)}' to CMYK PDF '{Path.GetFileName(fullOutputPdfPath)}' using ICC profile '{Path.GetFileName(fullIccProfilePath)}'...");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Ghostscript konnte nicht gestartet werden.");

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            bool outputWasCreated = File.Exists(fullOutputPdfPath);
            string message = outputWasCreated
                ? $"Ghostscript wurde mit Exit-Code {process.ExitCode} beendet. Die Ausgabe-PDF wurde zwar angelegt, sollte aber geprueft werden: {fullOutputPdfPath}"
                : $"Ghostscript wurde mit Exit-Code {process.ExitCode} beendet.";

            return PdfColorConversionResult.Failed(
                message,
                standardOutput,
                standardError,
                outputWasCreated ? fullOutputPdfPath : null);
        }

        if (!File.Exists(fullOutputPdfPath))
        {
            return PdfColorConversionResult.Failed(
                "Ghostscript wurde beendet, aber die CMYK-PDF wurde nicht erzeugt.",
                standardOutput,
                standardError,
                null);
        }

        return PdfColorConversionResult.Converted(fullOutputPdfPath, standardOutput, standardError);
    }

    private static string? ResolveGhostscriptExecutable(string? ghostscriptExecutablePath)
    {
        if (!string.IsNullOrWhiteSpace(ghostscriptExecutablePath))
        {
            string fullGhostscriptExecutablePath = Path.GetFullPath(ghostscriptExecutablePath);
            return File.Exists(fullGhostscriptExecutablePath)
                ? fullGhostscriptExecutablePath
                : ghostscriptExecutablePath;
        }

        foreach (string executableCandidate in GhostscriptExecutableCandidates)
        {
            if (CanStartGhostscript(executableCandidate))
            {
                return executableCandidate;
            }
        }

        return null;
    }

    private static bool CanStartGhostscript(string executablePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--version");

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            process.WaitForExit(3000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void AddGhostscriptArguments(
        ProcessStartInfo startInfo,
        string inputPdfPath,
        string outputPdfPath,
        string iccProfilePath)
    {
        startInfo.ArgumentList.Add("-dSAFER");
        startInfo.ArgumentList.Add($"--permit-file-read={iccProfilePath}");
        startInfo.ArgumentList.Add("-dBATCH");
        startInfo.ArgumentList.Add("-dNOPAUSE");
        startInfo.ArgumentList.Add("-sDEVICE=pdfwrite");
        startInfo.ArgumentList.Add("-dPDFSETTINGS=/prepress");
        startInfo.ArgumentList.Add("-dProcessColorModel=/DeviceCMYK");
        startInfo.ArgumentList.Add("-sColorConversionStrategy=CMYK");
        startInfo.ArgumentList.Add("-sColorConversionStrategyForImages=CMYK");
        startInfo.ArgumentList.Add("-dOverrideICC");
        startInfo.ArgumentList.Add($"-sOutputICCProfile={iccProfilePath}");
        startInfo.ArgumentList.Add($"-sOutputFile={outputPdfPath}");
        startInfo.ArgumentList.Add(inputPdfPath);
    }
}

public sealed record PdfColorConversionResult(
    PdfColorConversionStatus Status,
    string Message,
    string? OutputPdfPath = null,
    string? StandardOutput = null,
    string? StandardError = null)
{
    public static PdfColorConversionResult Converted(
        string outputPdfPath,
        string standardOutput,
        string standardError)
    {
        return new PdfColorConversionResult(
            PdfColorConversionStatus.Converted,
            $"CMYK-PDF erzeugt: {outputPdfPath}",
            outputPdfPath,
            standardOutput,
            standardError);
    }

    public static PdfColorConversionResult Skipped(string message)
    {
        return new PdfColorConversionResult(PdfColorConversionStatus.Skipped, message);
    }

    public static PdfColorConversionResult Failed(
        string message,
        string standardOutput,
        string standardError,
        string? outputPdfPath)
    {
        return new PdfColorConversionResult(
            PdfColorConversionStatus.Failed,
            message,
            outputPdfPath,
            StandardOutput: standardOutput,
            StandardError: standardError);
    }
}

public enum PdfColorConversionStatus
{
    Converted,
    Skipped,
    Failed
}
