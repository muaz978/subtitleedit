using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Platform;
using Nikse.SubtitleEdit.Core.AudioToText;
using Nikse.SubtitleEdit.Logic.Config;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.Engines;

/// <summary>
/// Speech-to-text engine for macOS that runs the pip-installed "faster-whisper" Python package
/// from the command line. Installation is detected by importing the package via python3, models
/// are downloaded automatically by faster-whisper on first use, and "--device auto" lets
/// CTranslate2 pick the fastest available backend (Apple Silicon acceleration where supported,
/// otherwise CPU).
/// </summary>
public class FasterWhisperMac : ISpeechToTextEngine
{
    public static string StaticName => "Faster Whisper Mac";
    public string Name => StaticName;
    public string Choice => WhisperChoice.FasterWhisperMac;
    public string Url => "https://github.com/SYSTRAN/faster-whisper";

    private const string ExecutableName = "faster-whisper";

    private static readonly string[] SupportedModelNames = { "tiny", "base", "small", "medium", "large-v2", "large-v3" };

    private static bool? _isFasterWhisperInstalled;

    public List<WhisperLanguage> Languages => WhisperLanguage.Languages.OrderBy(p => p.Name).ToList();

    public List<WhisperModel> Models
    {
        get
        {
            var models = new WhisperCTranslate2Model().Models;
            return models.Where(p => SupportedModelNames.Contains(p.Name)).ToList();
        }
    }

    public string Extension => string.Empty;
    public string UnpackSkipFolder => string.Empty;

    public bool IsEngineInstalled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        if (_isFasterWhisperInstalled.HasValue)
        {
            return _isFasterWhisperInstalled.Value;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("python3", "-c \"import faster_whisper\"")
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

#pragma warning disable CA1416
            process.Start();
            if (!process.WaitForExit(10_000))
            {
                process.Kill(true);
                _isFasterWhisperInstalled = false;
                return false;
            }
#pragma warning restore CA1416

            _isFasterWhisperInstalled = process.ExitCode == 0;
        }
        catch
        {
            _isFasterWhisperInstalled = false;
        }

        return _isFasterWhisperInstalled.Value;
    }

    public override string ToString()
    {
        return Name;
    }

    public string GetAndCreateWhisperFolder()
    {
        var baseFolder = Se.SpeechToTextFolder;
        if (!Directory.Exists(baseFolder))
        {
            Directory.CreateDirectory(baseFolder);
        }

        var folder = Path.Combine(baseFolder, "FasterWhisperMac");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        return folder;
    }

    public string GetAndCreateWhisperModelFolder(WhisperModel? whisperModel)
    {
        // faster-whisper downloads models itself into the Hugging Face cache.
        return new WhisperCTranslate2Model().ModelFolder;
    }

    public string GetExecutable()
    {
        // pip puts the "faster-whisper" console script in different places depending on how
        // Python was installed (Homebrew, python.org installer, "pip3 install --user", ...).
        var candidates = new List<string>
        {
            Path.Combine("/opt/homebrew/bin", ExecutableName),
            Path.Combine("/usr/local/bin", ExecutableName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", ExecutableName),
        };

        var libraryPythonFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Python");
        if (Directory.Exists(libraryPythonFolder))
        {
            candidates.AddRange(Directory.GetDirectories(libraryPythonFolder)
                .Select(dir => Path.Combine(dir, "bin", ExecutableName)));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return ExecutableName; // resolved via PATH
    }

    public bool IsModelInstalled(WhisperModel model)
    {
        // faster-whisper resolves models by name and downloads them on first use,
        // so any of the supported models can be used right away.
        return true;
    }

    public string GetModelForCmdLine(string modelName)
    {
        return modelName;
    }

    public async Task<string> GetHelpText()
    {
        var assetName = $"{StaticName.Replace(" ", string.Empty)}.txt";
        var uri = new Uri($"avares://SubtitleEdit/Assets/SpeechToText/{assetName}");

        try
        {
            await using var stream = AssetLoader.Open(uri);
            using var reader = new StreamReader(stream);
            var contents = await reader.ReadToEndAsync();
            return contents;
        }
        catch
        {
            return "Faster Whisper Mac runs the \"faster-whisper\" Python package from the command line.\n\n" +
                   "Install it with: pip3 install faster-whisper\n\n" +
                   "Models (tiny, base, small, medium, large-v2, large-v3) are downloaded automatically on first use.\n" +
                   "The device is set to \"auto\" so the fastest available backend is used on both Apple Silicon and Intel Macs.";
        }
    }

    public string GetWhisperModelDownloadFileName(WhisperModel whisperModel, string url)
    {
        return string.Empty;
    }

    public bool CanBeDownloaded()
    {
        return false;
    }

    public string CommandLineParameter
    {
        get => Se.Settings.Tools.AudioToText.CommandLineParameterFasterWhisperMac;
        set => Se.Settings.Tools.AudioToText.CommandLineParameterFasterWhisperMac = value;
    }
}
