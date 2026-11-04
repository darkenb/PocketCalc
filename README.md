# PocketCalc

Small in-game calculator in the game's own window style. In a game, `Alt+C` opens it; `Alt+C`, `Esc` or the window's X closes it. It is closed on the main menu and in the Esc menu. Use the keyboard or click the buttons. The hotkey is in `BepInEx/config/bug.dsp.pocketcalc.cfg`.

| Key | Action |
|---|---|
| Top-row digits, Numpad | Digit |
| `/ * - +` (Numpad and main keyboard, following your layout) | Operator |
| `.` `,` | Decimal separator |
| `Enter`, `Numpad Enter`, `=` | Result |
| `Backspace` | Deletes the last character |
| `Delete` | Clears |

Multiplication and division come first: `2+3*4` is 14. After a result, an operator continues from it and a digit starts a new calculation.

While it is open the keyboard belongs to the calculator: walking, mouse-wheel zoom and game and mod shortcuts stop, the mouse keeps working. Ctrl, Shift and Alt still reach the game (for shift-click). If you click another text field, keys go to that field.

Keys a mod reads some other way than `Input.GetKey` cannot be blocked (its own `OnGUI` events, Windows calls). A scan of the installed mods found no such reads.

## Building from source

You need a current .NET SDK and the .NET Framework 4.7.2 targeting pack. The game, Unity and BepInEx assemblies are referenced from your own installation and are not part of this repository.

```text
dotnet build -c Release "-p:DSPGamePath=D:\Games\Dyson Sphere Program" "-p:BepInExCorePath=C:\path\to\BepInEx\core"
```

The output is `bin/Release/net472/PocketCalc.dll`. Copy it to `BepInEx/plugins/PocketCalc/`.

## License

MIT, see [LICENSE](LICENSE).
