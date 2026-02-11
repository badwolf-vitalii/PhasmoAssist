# PhasmoAssist
Overlay assist for Phasmophobia

This tool is a WPF application written in C#. It requires the Microsoft .NET8 Runtime for Desktop being installed in your system. It is an open-source project published on GitHub. Hope you'll find it useful.

Даний інструмент – WPF-додаток, написаним на C#. Для його роботи у системі має бути встановлено Microsoft .NET8 Runtime для Desktop. Це проєкт із відкритим вихідним кодом, опублікований на GitHub. Сподіваюся, він буде для вас корисним.

СТОРІНКА ПРОЕКТУ: https://github.com/VitoMilanese/PhasmoAssist

KEY BINDINGS

F1 – start/stop a timer. A timer font size gets bigger after 20 and 100 seconds and the font foreground color gets red after 25 and 180 seconds (it turns back to white after 100 seconds). Timer always starts from 0.
XMouse1 - does the same F1 does.
XMouse2 - Restores the timer, if it's been stopped accidentially. Timer continues taking into account all the time elapsed after the moment when it's been stopped.

F2 – change language
Now, there are only five dictionaries: Ukrainian, English, Italian, French, Russian. You can cycle through the languages by pressing F2. You can also add a new language – just create a new TXT file in the directory Languages and fill it in with new translation. The lastly activated language will be selected automatically on the next start.

F4 – shutdown the assist. If you close the game by pressing ALT+F4, the assist will be also closed because you press F4.

F5 – show About. You always see on start.

F10 – hide/show the overlay

0 – hide/show only evidences and ghosts but leave the timer.

1 – (un)mark the D.O.T.S.
2 – (un)mark the EMF 5
3 – (un)mark the Freezing Temperatures
4 – (un)mark the Ghost Orb
5 – (un)mark the Ghost Writing
6 – (un)mark the Spirit Box
7 – (un)mark the Ultraviolet

~ (the key to the left of 1) – unmark all evidences. Press it again to restore the evidences marked before reset. The key [ ~ ] may look like [ ` ] on your keyboard – depends on the keyboard model.

L.CTRL + 1 ... L.CTRL + 9,
L.CTRL + Q ... L.CTRL + Y,
L.CTRL + A ... L.CTRL + H,
L.CTRL + Z ... L.CTRL + N - exclude a ghost from the list.
You can switch the L.CTRL button to L.ALT button in the config.cfg by replacing 0 with 1 in the 12th line.

Also you can change the text size in the config.cfg file.

---------------------------------------------------

ПРИЗНАЧЕННЯ КЛАВІШ

F1 – запуск/зупинка таймера. Розмір шрифту таймера збільшується після 20 і 100 секунд, а колір шрифту стає червоним після 25 і 180 секунд (він стає знову білим після 100 секунд). Таймер запускається завжди з 0.
XMouse1 - Те ж саме, що й F1.
XMouse2 - Відновити таймер, якщо він був зупинений випадково. Таймер продовжиться, враховуючи також увесь час, після його деактивації.

F2 – зміна мови. Наразі доступні лише п’ять мов: українська, англійська, італійська, французька, російська. Ви можете перемикати мови по колу, натискаючи F2. Також ви можете додати нову мову – просто створіть новий TXT-файл у папці Languages та заповніть його новим перекладом. Остання активована мова буде вибрана автоматично під час наступного запуску.

F4 – закриття асиста. Якщо ви закриваєте гру комбінацією ALT+F4, асист також буде закрито, оскільки при цьому натискається F4.

F5 – показати інформацію «Про програму». Ви бачите її щоразу під час запуску.

F10 – приховати/показати оверлей

0 – приховати/показати лише докази та привидів, але залишити таймер.

1 – (зняти/позначити) D.O.T.S.
2 – (зняти/позначити) EMF 5
3 – (зняти/позначити) Мінусова Температура
4 – (зняти/позначити) Примарний Вогник
5 – (зняти/позначити) Примарні Записи
6 – (зняти/позначити) Радіоприймач
7 – (зняти/позначити) Ультрафіолет

~ (клавіша ліворуч від 1) – скинути всі докази. Натисніть ще раз, щоб відновити докази, які були позначені до скидання. Клавіша [ ~ ] може виглядати на вашій клавіатурі як [ ` ] – залежить від моделі клавіатури.

L.CTRL + 1 ... L.CTRL + 9,
L.CTRL + Q ... L.CTRL + Y,
L.CTRL + A ... L.CTRL + H,
L.CTRL + Z ... L.CTRL + N - виключити привида зі списку.
Ви можете змінити L.CTRL на L.ALT у файлі config.cfg, замінивши 0 на 1 у 12 рядку.

Також у файлі config.cfg можна змінити розмір тексту.