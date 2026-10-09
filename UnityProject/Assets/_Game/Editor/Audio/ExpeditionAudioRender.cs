using System;
using System.Globalization;
using System.IO;
using System.Text;
using JungleBooze.App.Expedition;
using JungleBooze.Core.Save;
using JungleBooze.Editor.Expedition;
using JungleBooze.Gameplay.Run;
using JungleBooze.Gameplay.World;
using JungleBooze.Services.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JungleBooze.Editor.Audio
{
    /// <summary>
    /// Audio log of the Expedition bot run, frame-aligned with <see cref="ExpeditionVideo"/> (same save, seed, bot
    /// routes, 2 ticks per 30 fps frame, same ending), for the demo video with sound:
    ///   tools/ci/unity.sh method JungleBooze.Editor.Audio.ExpeditionAudioRender.Capture [-jbAudioOut /tmp/junglebooze-video] [-jbUntil 2860]
    ///   python3 tools/assetgen/audio_offline_mix.py /tmp/junglebooze-video/expedition_audio.jsonl → wav → ffmpeg mux.
    /// Every decision of the real audio code (cues with the chosen variation and pitch, stings, ambience fades, music
    /// starts/levels/stop/results) is logged on the simulation clock: an event at frame k is at k/30 s in the video.
    /// The music starts with its intro, as on the first run of a session.
    /// </summary>
    public static class ExpeditionAudioRender
    {
        private const string LogPrefix = "[JungleBooze] ";
        private const int TicksPerFrame = 2;
        private const int MaxTicks = 60 * 60 * 6;
        private const int EndingMaxTicks = 60 * 60;
        private const int ResultsSeconds = 5;

        private static int s_ticks;

        public static void Capture()
        {
            int code;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string outDir = Arg(args, "-jbAudioOut") ?? "/tmp/junglebooze-video";
                float until = float.Parse(Arg(args, "-jbUntil") ?? "2860", CultureInfo.InvariantCulture);
                code = Run(outDir, until);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "Audio render failed: " + exception);
                code = 1;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }

        private static int Run(string outDir, float until)
        {
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(ExpeditionPaths.Scene, OpenSceneMode.Single);
            ExpeditionRoot root = Object.FindFirstObjectByType<ExpeditionRoot>();
            if (root == null)
            {
                Debug.LogError(LogPrefix + "ExpeditionRoot not found.");
                return 2;
            }

            s_ticks = 0;
            root.UseMemorySave(SaveData.CreateDefault(2026L, -0.3f));
            root.EnableToolsAudio(() => s_ticks / 60.0);
            root.Build();
            if (root.Audio == null)
            {
                Debug.LogError(LogPrefix + "No audio catalog.");
                return 2;
            }

            var log = new JsonAudioLog();
            root.SetCameraProfile(true, true);
            root.SetBotDriving(true);
            root.SetBotRoutes(RouteType.Secret, RouteType.Risky, RouteType.Safe);
            root.SetDebugVisible(false);
            root.Audio.Service.Log = log;
            root.StartRun();
            root.Audio.Service.MusicStart(true);

            var frames = new StringBuilder(1 << 20);
            int ticks = 0;
            int frame = 0;
            int endTick = -1;
            int resultsFrames = -1;
            while (ticks < MaxTicks && resultsFrames < ResultsSeconds * 30)
            {
                if (root.Simulation.State.Distance >= until && !root.Simulation.State.Dead && endTick < 0)
                {
                    endTick = ticks;
                    root.SetBotDriving(false);
                }

                if (endTick >= 0 && ticks - endTick > EndingMaxTicks && root.Session.Phase != RunPhase.Results && !root.Simulation.State.Dead)
                {
                    break;
                }

                if (root.Session.Phase == RunPhase.Results && root.LastResults != null)
                {
                    resultsFrames++;
                }

                s_ticks = ticks;
                root.StepTicks(TicksPerFrame);
                ticks += TicksPerFrame;
                frames.AppendFormat(CultureInfo.InvariantCulture, "{0} t {1:0.00} s {2:0.0}\n", frame, frame / 30f, root.Simulation.State.Distance);
                frame++;
            }

            log.End(frame / 30.0);
            string file = Path.Combine(outDir, "expedition_audio.jsonl");
            File.WriteAllText(file, log.ToString());
            File.WriteAllText(Path.Combine(outDir, "expedition_audio.frames.txt"), frames.ToString());
            Debug.Log(LogPrefix + "Audio log " + file + ": " + log.Count + " entries, " + frame + " frames (" + (frame / 30f).ToString("0.0", CultureInfo.InvariantCulture) + " s).");
            root.Audio.Stop();
            return 0;
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name)
                {
                    return i + 1 < args.Length ? args[i + 1] : string.Empty;
                }
            }

            return null;
        }

        private sealed class JsonAudioLog : IAudioLog
        {
            private readonly StringBuilder _b = new StringBuilder(1 << 18);

            public int Count { get; private set; }

            public void Cue(double time, string id, int clip, float semitones, float gain)
            {
                Line(string.Format(CultureInfo.InvariantCulture, "{{\"t\":{0:0.0000},\"type\":\"cue\",\"id\":\"{1}\",\"clip\":{2},\"semis\":{3:0.000},\"gain\":{4:0.000}}}", time, id, clip, semitones, gain));
            }

            public void Sting(double time, string id)
            {
                Line(string.Format(CultureInfo.InvariantCulture, "{{\"t\":{0:0.0000},\"type\":\"sting\",\"id\":\"{1}\"}}", time, id));
            }

            public void Ambience(double time, string channel, string id, float gain, float fade)
            {
                Line(string.Format(CultureInfo.InvariantCulture, "{{\"t\":{0:0.0000},\"type\":\"amb\",\"channel\":\"{1}\",\"id\":{2},\"gain\":{3:0.000},\"fade\":{4:0.000}}}", time, channel, id == null ? "null" : "\"" + id + "\"", gain, fade));
            }

            public void Music(double time, string action, int value)
            {
                Line(string.Format(CultureInfo.InvariantCulture, "{{\"t\":{0:0.0000},\"type\":\"music\",\"action\":\"{1}\",\"value\":{2}}}", time, action, value));
            }

            public void End(double time)
            {
                Line(string.Format(CultureInfo.InvariantCulture, "{{\"t\":{0:0.0000},\"type\":\"end\"}}", time));
            }

            public override string ToString() => _b.ToString();

            private void Line(string s)
            {
                _b.Append(s).Append('\n');
                Count++;
            }
        }
    }
}
