# sherpa-onnx bindings

Apache-2.0, sherpa-onnx 2.0.1, source revision `af2d8d81dbfa61c4cea4c568da78f3982edca005`.
Source: https://github.com/k2-fsa/sherpa-onnx/tree/af2d8d81dbfa61c4cea4c568da78f3982edca005/scripts/dotnet

The full native configuration structs are required for ABI compatibility, including unused model families. Murmur changes string marshalling from LPStr to LPUTF8Str so non-ASCII Windows user paths work. The checked handles and decoder wrapper in Services/FastSpeechEngine.cs are independently implemented. Original license is included.

Moonshine English base weights: copyright 2025 Useful Sensors, Inc./Moonshine AI, MIT. See MOONSHINE-MODEL-LICENSE. Public quantized conversion: https://huggingface.co/csukuangfj2/sherpa-onnx-moonshine-base-en-quantized-2026-02-27/tree/8f4d6c58c03d40bcea40043bb7120a878f2bbef6 . No non-English Moonshine weights are used.
