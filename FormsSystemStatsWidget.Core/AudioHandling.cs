using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace FormsSystemStatsWidget.Core
{
    public class AudioHandling : IAsyncDisposable
    {
        public static string ExportDirectory { get; set; } = Path.GetFullPath(Environment.GetEnvironmentVariable("SHARPAI_AUDIO_EXPORT_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "SharpAI_AudioExports"));

        public readonly BindingList<AudioObj> Audios = [];

        private CancellationTokenSource? recordingCts;


        public AudioObj? this[Guid id] => this.Audios.FirstOrDefault(a => a.Id == id);
        public AudioObj? this[int index] => (index >= 0 && index < this.Audios.Count) ? this.Audios[index] : null;
        public AudioObj? this[string name, bool fuzzyMatch = true] => fuzzyMatch ? this.Audios.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) : this.Audios.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

        public bool IsRecording => this.recordingCts != null && this.recordingCts?.IsCancellationRequested == false;

        public AudioHandling(string? customExportDir = null, string[]? additionalRessourcePaths = null)
        {
            if (!string.IsNullOrEmpty(customExportDir))
            {
                ExportDirectory = Path.GetFullPath(customExportDir);
            }

            if (additionalRessourcePaths != null)
            {
                foreach (var path in additionalRessourcePaths)
                {
                    this.ImportResourcePath(path);
                }
            }
        }


        public bool AddAudio(AudioObj audioObj)
        {
            this.Audios.Add(audioObj);
            return true;
        }

        public AudioObj? ImportAudio(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            if (!IsSupportedAudioExtension(filePath))
            {
                return null;
            }

            AudioObj? audioObj = null;
            try
            {
                audioObj = new AudioObj(filePath);
                this.AddAudio(audioObj);
            }
            catch
            {
                audioObj = null;
            }

            return audioObj;
        }

        public async Task<AudioObj?> ImportAudioAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var audioObj = new AudioObj(filePath);
                    this.AddAudio(audioObj);
                    return audioObj;
                }
                catch
                {
                    return null;
                }
            });
        }


        public int FindActiveMicrophoneIndex()
        {
            int deviceCount = WaveInEvent.DeviceCount;
            if (deviceCount == 0)
            {
                return -1;
            }

            int bestDevice = 0;
            float maxPeak = 0f;

            for (int i = 0; i < deviceCount; i++)
            {
                float currentPeak = this.TestDevicePeak(i);
                if (currentPeak > maxPeak)
                {
                    maxPeak = currentPeak;
                    bestDevice = i;
                }
            }

            return bestDevice;
        }

        private float TestDevicePeak(int deviceIndex)
        {
            float peak = 0;
            using var waveIn = new WaveInEvent { DeviceNumber = deviceIndex, WaveFormat = new WaveFormat(44100, 1) };

            waveIn.DataAvailable += (s, e) =>
            {
                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i);
                    float sampleFloat = Math.Abs(sample / 32768f);
                    if (sampleFloat > peak)
                    {
                        peak = sampleFloat;
                    }
                }
            };

            waveIn.StartRecording();
            Thread.Sleep(200);
            waveIn.StopRecording();

            return peak;
        }


        public async Task<AudioObj?> RecordAudioAsync(int? deviceIndex = null, int sampleRate = 44100, int bitDepth = 16, int channels = 2, Action<float>? onLevel = null)
        {
            var wf = new WaveFormat(sampleRate, bitDepth, channels);
            return await this.RecordAudioAsync(wf, deviceIndex, onLevel).ConfigureAwait(false);
        }


        public async Task<AudioObj?> RecordAudioAsync(WaveFormat waveFormat, int? deviceIndex = null, Action<float>? onLevel = null)
        {
            if (!this.TryResolveRecordingDeviceIndex(ref deviceIndex))
            {
                return null;
            }

            if (!this.TryStartRecordingSession(out CancellationToken ct))
            {
                return null;
            }

            var tcs = new TaskCompletionSource<AudioObj>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sampleList = new List<float>();
            var bytesPerSample = waveFormat.BitsPerSample / 8;

            using var waveIn = new WaveInEvent
            {
                DeviceNumber = deviceIndex.Value,
                WaveFormat = waveFormat
            };

            waveIn.DataAvailable += (s, e) =>
            {
                float peak = AppendSamples(e.Buffer, e.BytesRecorded, bytesPerSample, sampleList);
                try { onLevel?.Invoke(Math.Clamp(peak, 0f, 1f)); } catch { }
            };

            waveIn.RecordingStopped += (s, e) =>
            {
                string name = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}";
                var audioObj = new AudioObj(sampleList.ToArray(), waveIn.WaveFormat.SampleRate, waveIn.WaveFormat.Channels, waveFormat.BitsPerSample, name);
                tcs.TrySetResult(audioObj);
            };

            waveIn.StartRecording();
            Logger.Log($"Recording started on device {deviceIndex.Value} with format {waveFormat.SampleRate}Hz, {waveFormat.BitsPerSample}bit, {waveFormat.Channels}ch");

            return await this.AwaitRecordingCompletionAsync(waveIn, tcs, ct, "Recording stopped. Processing audio...").ConfigureAwait(false);
        }

        public async Task<AudioObj?> RecordAudioAutoAsync(double? autoStopSilenceSeconds = null, CancellationToken ct = default)
        {
            int deviceIndex = this.FindActiveMicrophoneIndex();
            if (deviceIndex == -1)
            {
                Logger.Log("No recording devices found.");
                return null;
            }

            if (!this.TryStartRecordingSession(ct, out CancellationToken linkedToken))
            {
                return null;
            }

            var waveFormat = new WaveFormat(16000, 16, 1);

            var tcs = new TaskCompletionSource<AudioObj>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sampleList = new List<float>();

            int bytesPerSample = waveFormat.BitsPerSample / 8;
            var silenceTracker = new SilenceTracker();

            using var waveIn = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = waveFormat
            };

            waveIn.DataAvailable += (s, e) =>
            {
                float peak = AppendSamples(e.Buffer, e.BytesRecorded, bytesPerSample, sampleList);
                this.UpdateSilenceTracking(silenceTracker, peak, e.BytesRecorded, waveFormat, autoStopSilenceSeconds);
            };

            waveIn.RecordingStopped += (s, e) =>
            {
                string name = $"AutoRec_{DateTime.Now:yyyyMMdd_HHmmss}";
                var audioObj = new AudioObj(sampleList.ToArray(), waveIn.WaveFormat.SampleRate, waveIn.WaveFormat.Channels, waveIn.WaveFormat.BitsPerSample, name);
                tcs.TrySetResult(audioObj);
            };

            waveIn.StartRecording();
            Logger.Log($"Auto-Recording started on device {deviceIndex} with format {waveFormat.SampleRate}Hz, {waveFormat.BitsPerSample}bit, {waveFormat.Channels}ch");

            return await this.AwaitRecordingCompletionAsync(waveIn, tcs, linkedToken, null).ConfigureAwait(false);
        }

        private sealed class SilenceTracker
        {
            public double CurrentSilenceSeconds;
            public const float SilenceThreshold = 0.015f;
        }

        private static float AppendSamples(byte[] buffer, int bytesRecorded, int bytesPerSample, List<float> sampleList)
        {
            int step = bytesPerSample;
            float peak = 0f;

            for (int i = 0; i + (step - 1) < bytesRecorded; i += step)
            {
                if (!TryReadSample(buffer, i, bytesPerSample, out float sample))
                {
                    continue;
                }

                sampleList.Add(sample);
                peak = Math.Max(peak, Math.Abs(sample));
            }

            return peak;
        }

        private void UpdateSilenceTracking(
            SilenceTracker tracker,
            float peak,
            int bytesRecorded,
            WaveFormat waveFormat,
            double? autoStopSilenceSeconds)
        {
            if (!autoStopSilenceSeconds.HasValue)
            {
                return;
            }

            if (peak > SilenceTracker.SilenceThreshold)
            {
                tracker.CurrentSilenceSeconds = 0.0;
                return;
            }

            double chunkDuration = (double) bytesRecorded / waveFormat.AverageBytesPerSecond;
            tracker.CurrentSilenceSeconds += chunkDuration;

            if (tracker.CurrentSilenceSeconds >= autoStopSilenceSeconds.Value && !this.recordingCts.IsCancellationRequested)
            {
                Logger.Log($"Silence of {autoStopSilenceSeconds}s detected. Stopping recording.");
                this.recordingCts.Cancel();
            }
        }

        private async Task<AudioObj?> AwaitRecordingCompletionAsync(
            WaveInEvent waveIn,
            TaskCompletionSource<AudioObj> tcs,
            CancellationToken cancellationToken,
            string? stoppedLogMessage)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                StopWaveInSafely(waveIn);
            }

            if (stoppedLogMessage != null)
            {
                Logger.Log(stoppedLogMessage);
            }

            var result = await tcs.Task.ConfigureAwait(false);
            this.EndRecordingSession();

            return result;
        }



        public bool StopRecording()
        {
            if (this.recordingCts == null)
            {
                return false;
            }

            try
            {
                this.recordingCts.Cancel();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ImportResourcePath(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    this.ImportAudio(file);
                }

                return;
            }

            if (File.Exists(path))
            {
                this.ImportAudio(path);
            }
        }

        private static bool IsSupportedAudioExtension(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".flac", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryResolveRecordingDeviceIndex(ref int? deviceIndex)
        {
            if (deviceIndex.HasValue)
            {
                return true;
            }

            int resolvedDeviceIndex = this.FindActiveMicrophoneIndex();
            if (resolvedDeviceIndex == -1)
            {
                Logger.Log("No recording devices found.");
                return false;
            }

            deviceIndex = resolvedDeviceIndex;
            return true;
        }

        private bool TryStartRecordingSession(out CancellationToken token)
        {
            token = default;
            if (this.recordingCts != null)
            {
                Logger.Log("Recording already in progress.");
                return false;
            }

            this.recordingCts = new CancellationTokenSource();
            token = this.recordingCts.Token;
            return true;
        }

        private bool TryStartRecordingSession(CancellationToken externalToken, out CancellationToken token)
        {
            token = default;
            if (this.recordingCts != null)
            {
                Logger.Log("Recording already in progress.");
                return false;
            }

            this.recordingCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            token = this.recordingCts.Token;
            return true;
        }

        private void EndRecordingSession()
        {
            this.recordingCts?.Dispose();
            this.recordingCts = null;
        }

        private static void StopWaveInSafely(WaveInEvent waveIn)
        {
            try
            {
                waveIn.StopRecording();
            }
            catch
            {
            }
        }

        private static bool TryReadSample(byte[] buffer, int offset, int bytesPerSample, out float sample)
        {
            sample = 0f;

            try
            {
                if (bytesPerSample == 1)
                {
                    byte b = buffer[offset];
                    sample = (b - 128) / 128f;
                    return true;
                }

                if (bytesPerSample == 2)
                {
                    short s16 = BitConverter.ToInt16(buffer, offset);
                    sample = s16 / 32768f;
                    return true;
                }

                if (bytesPerSample == 4)
                {
                    int s32 = BitConverter.ToInt32(buffer, offset);
                    sample = s32 / 2147483648f;
                    return true;
                }

                short fallback = BitConverter.ToInt16(buffer, offset);
                sample = fallback / 32768f;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static TimeSpan? GetAudioDuration(string filePath)
        {
            try
            {
                using var reader = new AudioFileReader(filePath);
                return reader.TotalTime;
            }
            catch
            {
                return null;
            }
        }




        public bool RemoveAudio(AudioObj audioObj)
        {
            if (this.Audios.Contains(audioObj))
            {
                this.Audios.Remove(audioObj);
                return true;
            }

            return false;
        }

        public bool RemoveAudio(Guid audioId)
        {
            var audioObj = this.Audios.FirstOrDefault(a => a.Id == audioId);
            if (audioObj != null)
            {
                this.Audios.Remove(audioObj);
                return true;
            }
            return false;
        }

        public bool RemoveAudio(string name, bool fuzzyMatch = false)
        {
            AudioObj? audioObj;
            if (fuzzyMatch)
            {
                audioObj = this.Audios.FirstOrDefault(a => a.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                audioObj = this.Audios.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            }
            if (audioObj != null)
            {
                this.Audios.Remove(audioObj);
                return true;
            }
            return false;
        }


        public int ClearAudios()
        {
            int count = this.Audios.Count;
            foreach (var audio in this.Audios)
            {
                audio.Dispose();
            }
            return count;
        }

        public async Task ClearAudiosAsync()
        {
            var disposeTasks = this.Audios.Select(a => Task.Run(() => a.Dispose())).ToArray();
            await Task.WhenAll(disposeTasks);
            this.Audios.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            var disposeTasks = this.Audios.Select(a => Task.Run(() => a.Dispose())).ToArray();

            await Task.WhenAll(disposeTasks);

            this.Audios.Clear();

            GC.SuppressFinalize(this);
        }
    }
}
