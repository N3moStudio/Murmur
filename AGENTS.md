# Murmur contributor instructions

Read docs/parity.md and docs/build-release.md before changing product scope. Keep implementation, automated checks and physical-device evidence separate. Never mark missing functionality complete.

Preserve user data, microphone privacy, clipboard ownership and changed editor text. Do not log transcripts or credentials. Microphones and orb timers must stop while idle. Ordinary dictation selects its editor at stop and retains recovery when insertion is unsafe. Never auto-submit messages.

Keep licensed third-party notices. Exclude recordings, private profiles, credentials, build outputs and proprietary reference captures from commits and releases. Treat the Mac implementation as a preview until its documented device checks pass.
