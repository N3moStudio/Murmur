using System.Diagnostics;
using System.Text.Json;
using SherpaOnnx;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

string family = args[0], root = args[1], output = args[3];
int threads = int.Parse(args[2]);
var config = new OfflineRecognizerConfig();
config.ModelConfig.NumThreads = threads;
config.ModelConfig.Provider = "cpu";
config.ModelConfig.Tokens = Path.Combine(root, "tokens.txt");
config.DecodingMethod = "greedy_search";
if (family == "parakeet")
{
    config.FeatConfig.FeatureDim = 128;
    config.ModelConfig.Transducer.Encoder = Path.Combine(root, "encoder.int8.onnx");
    config.ModelConfig.Transducer.Decoder = Path.Combine(root, "decoder.int8.onnx");
    config.ModelConfig.Transducer.Joiner = Path.Combine(root, "joiner.int8.onnx");
    config.ModelConfig.ModelType = "nemo_transducer";
}
else
{
    config.ModelConfig.Moonshine.Encoder = Path.Combine(root, "encoder_model.ort");
    config.ModelConfig.Moonshine.MergedDecoder = Path.Combine(root, "decoder_model_merged.ort");
}
var watch = Stopwatch.StartNew();
using var recognizer = new OfflineRecognizer(config);
double loadMs = watch.Elapsed.TotalMilliseconds;
var results = new List<object>();
foreach (string file in args.Skip(4))
{
    using var reader = new AudioFileReader(file);
    ISampleProvider mono = reader.WaveFormat.Channels == 2 ? new StereoToMonoSampleProvider(reader) : reader;
    var source = new WdlResamplingSampleProvider(mono, 16000);
    var samples = new List<float>(); var buffer = new float[16000]; int count;
    while ((count = source.Read(buffer, 0, buffer.Length)) > 0) samples.AddRange(buffer.Take(count));
    var pcm = samples.ToArray();
    for (int iteration = 0; iteration < 2; iteration++)
    {
        watch.Restart();
        using var stream = recognizer.CreateStream();
        stream.AcceptWaveform(16000, pcm); recognizer.Decode(stream);
        var result = stream.Result;
        results.Add(new { file = Path.GetFileName(file), iteration, seconds = pcm.Length / 16000d, milliseconds = watch.Elapsed.TotalMilliseconds, text = result.Text });
    }
}
var process = Process.GetCurrentProcess(); process.Refresh();
File.WriteAllText(output, JsonSerializer.Serialize(new { family, threads, loadMs, workingSet = process.WorkingSet64, peakWorkingSet = process.PeakWorkingSet64, results }, new JsonSerializerOptions { WriteIndented = true }));
