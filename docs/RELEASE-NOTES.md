<!--
  Release notes for NeuroMita.NudeMita.

  Kept as a file rather than typed into a shell command on purpose: non-ASCII text passed as a
  command argument is re-encoded to the system code page and comes out as question marks, which is
  exactly what happened to the first attempt at this release page. The publishing step reads this
  file as UTF-8 instead.
-->

# NeuroMita.NudeMita 0.1.0

## Русский

Ню-мод для Мит одним самодостаточным плагином BepInEx. Другие плагины моделей не нужны: плагин
сам читает контейнер мода и сам его дорабатывает.

### Установка

1. Скачайте пакет ню-мода со страницы мода — <https://www.nexusmods.com/miside/mods/58> — и
   положите файл (**имя файла любое**) в `<игра>\BepInEx\plugins\`
2. Распакуйте архив этого плагина в папку игры (туда, где `NeuroMita.exe`) и запустите
   `install.bat`
3. Запустите игру

Сам пакет в архив **не входит**: это чужая работа. Если положить его позже, перезапускать игру не
нужно — плагин ищет его каждые несколько секунд.

### Что закрыто

* **Пакет читается самостоятельно.** Unity-овский AssetBundle API в этой сборке нерабочий, поэтому
  контейнер `UnityFS` разбирается вручную: меши, bind pose, имена костей, материалы, текстуры.
* **Меш пакета — это три подмеша** (ошейник `Cloth`, тело `body_nsfw`, деталь шеи `Body`), а
  загрузчик контейнера склеивает их в один, и один подмеш несёт только один материал. Плагин
  возвращает границы пакета (320 / 30471 / 40 треугольников) и выдаёт каждой части ту карту, под
  которую её UV и разворачивали.
* **Одежда игры скрывается** в обоих написаниях, которые использует игра (`SweaterSlot` / `Sweater`
  и так далее).
* **Область действия — по пути трансформа.** Рендерер тела игрока тоже называется `Body`, а корень
  персонажа — `Mita Crazy _legacy`, а не `Mita Crazy`, поэтому ни имена, ни `GameObject.Find` тут не
  работают.

### Проверено офлайн, без запуска игры

* `tools/verify-release.ps1` — 49 проверок: версии (csproj / исходник / собранная сборка),
  идентичность `BepInPlugin`, состав ссылок сборки (нет `Assembly-CSharp`, нет других плагинов
  NeuroMita, нет assimp), гигиена исходников, разбор всех скриптов, содержимое архива, разделители
  в именах записей, переводы строк и кодировки
* `tools/verify-bindpose-math.ps1` — численное доказательство формулы bind pose: исправленный
  порядок даёт единичную матрицу скиннинга с точностью `1e-16`, прежний смещает вершину на 1.8
  единицы сцены при теле высотой 1.67
* `tools/bundledump` — читает пакет офлайн, поэтому вшитый профиль сверен с настоящим контейнером:
  `pack=320,30471,40  baked=320,30471,40`, обе текстуры на месте

## English

The nude mod for Mita as one self-contained BepInEx plugin. No other model plugin is involved: it
reads the pack's container itself and finishes it.

### Install

1. Download the nude mod pack from the mod page — <https://www.nexusmods.com/miside/mods/58> — and
   drop the file (**any file name**) into `<game>\BepInEx\plugins\`
2. Extract this plugin's archive into the game folder (the one holding `NeuroMita.exe`) and run
   `install.bat`
3. Launch the game

The pack itself is deliberately **not** in the archive: it is somebody else's work. Dropping it in
later works too — the plugin looks for it every few seconds, so the game does not need restarting.

### What it handles

* **Reads the pack itself.** Unity's AssetBundle API is unusable on this build, so the `UnityFS`
  container is parsed by hand: meshes, bind poses, bone names, materials, textures.
* **The pack's mesh is three submeshes** (the choker on `Cloth`, the body on `body_nsfw`, a neck
  piece on `Body`) and a container loader welds them into one, where a single submesh can carry only
  one material. The plugin puts the pack's boundaries back (320 / 30471 / 40 triangles) and gives
  each part the atlas its UVs were authored against.
* **The game's own clothing is hidden** in both spellings the game uses (`SweaterSlot` / `Sweater`,
  and so on).
* **Scope is decided by transform path.** The player's body renderer is called `Body` too, and the
  character root is `Mita Crazy _legacy` rather than `Mita Crazy`, so neither renderer names nor
  `GameObject.Find` can tell them apart.

### Verified offline, with no game launch

* `tools/verify-release.ps1` — 49 checks: version agreement across csproj, source and built
  assembly; the `BepInPlugin` identity; what the assembly links against (no `Assembly-CSharp`, no
  other NeuroMita plugin, no assimp); source hygiene; every script parses; and the archive's
  contents, entry separators, line endings and encodings
* `tools/verify-bindpose-math.ps1` — a numerical proof of the bind-pose formula: the corrected
  order reproduces the skinning identity to `1e-16`, while the old order displaces a vertex by 1.8
  scene units on a body 1.67 tall
* `tools/bundledump` — reads the pack offline, so the baked profile is checked against the real
  container: `pack=320,30471,40  baked=320,30471,40`, both textures present

## 中文

米塔裸体 mod —— 一个自包含的 BepInEx 插件。不需要其他模型插件：它自己解析 mod 包并完成收尾。

### 安装

1. 从 mod 主页 <https://www.nexusmods.com/miside/mods/58> 下载裸体 mod 包，把文件（**文件名随意**）
   放进 `<游戏>\BepInEx\plugins\`
2. 把本插件的压缩包解压到游戏目录（含 `NeuroMita.exe` 那层），运行 `install.bat`
3. 启动游戏

包本体**不在**压缩包内（那是别人的作品）。之后再放也可以，**不需要重启游戏** —— 插件每几秒重新查找。

### 它处理了什么

* **自己解析包**。这套构建里 Unity 的 AssetBundle API 不可用，所以 `UnityFS` 容器是手工解析的：
  网格、bind pose、骨骼名、材质、贴图。
* **包的网格是三个子网格**（项圈 `Cloth`、身体 `body_nsfw`、颈部件 `Body`），而容器加载器会把它们
  焊成一个，一个子网格只能有一个材质。插件按包自己的边界（320 / 30471 / 40 个三角形）拆回去，并给
  每部分它 UV 所针对的那张图集。
* **隐藏游戏自己的衣服**，两种命名（`SweaterSlot` / `Sweater` 等）都覆盖。
* **作用域按完整变换路径判定**。玩家的身体渲染器也叫 `Body`，角色根叫 `Mita Crazy _legacy` 而不是
  `Mita Crazy`，所以渲染器名字和 `GameObject.Find` 都无法区分。

### 已完成的离线验证（不启动游戏）

* `tools/verify-release.ps1` —— 49 项检查：版本一致性（csproj / 源码 / 编译产物）、`BepInPlugin`
  身份、程序集引用构成（无 `Assembly-CSharp`、无其他 NeuroMita 插件、无 assimp）、源码卫生、
  全部脚本语法、压缩包内容、条目分隔符、换行符与编码
* `tools/verify-bindpose-math.ps1` —— bind pose 公式的数值证明：修正后的顺序以 `1e-16` 精度还原
  蒙皮单位阵，原顺序在 1.67 米身体上把顶点挪走 1.8 个场景单位
* `tools/bundledump` —— 离线读取包，内置档案已与真实容器核对：`pack=320,30471,40  baked=320,30471,40`，
  两张贴图均在
