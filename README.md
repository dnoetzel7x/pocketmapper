# PocketMapper

Minimal external-display test app.

## Android

Uses `DisplayManager` and the Android `Presentation` API. The phone remains the
controller while a connected presentation display shows a test pattern or a
full black frame.

## Windows

The Windows controller automatically detects a second screen or projector. It
opens a borderless fullscreen output on that display while the controls remain
on the laptop or PC. The portable `PocketMapper.exe` requires no installation.
