# PocketCalc

Oyun içi küçük hesap makinesi. Yalnız oyundayken `Alt+C` ile açılır (ana menü ve Esc menüsünde kapalıdır), `Alt+C`, `Esc` veya pencerenin X düğmesiyle kapanır. Klavyeyle veya penceredeki düğmelerle fareyle kullanılır. Kısayol `BepInEx/config/bug.dsp.pocketcalc.cfg` dosyasındadır.

| Tuş | İşlev |
|---|---|
| Üst sıra rakamlar, Numpad | Rakam |
| `/ * - +` (Numpad ve ana klavye, klavye düzeninize göre) | İşlem |
| `.` `,` | Ondalık ayırıcı |
| `Enter`, `Numpad Enter`, `=` | Sonuç |
| `Backspace` | Son karakteri siler |
| `Delete` | Temizler |

Çarpma ve bölme önce yapılır: `2+3*4` sonucu 14'tür. Sonuçtan sonra işlem tuşuna basarsanız sonuçla devam eder, rakama basarsanız yeni işlem başlar.

Pencere açıkken klavye hesap makinesine aittir: mekayı yürütme, fare tekerleğiyle yakınlaştırma ve oyun ile mod kısayolları durur, fare çalışır. Ctrl, Shift ve Alt oyuna geçmeye devam eder (Shift ile tıklama için). Başka bir yazı alanına tıklarsanız tuşlar o alana gider.

Bir modun `Input.GetKey` dışında bir yoldan okuduğu tuşlar engellenemez (kendi `OnGUI` olayları, Windows çağrıları); kurulu modların taramasında böyle bir okuma bulunmadı.
