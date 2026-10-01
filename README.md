# NeuroMita.NudeMita

<!-- ====================================================================== -->
<!--  РУССКИЙ  (основной язык этого документа)                              -->
<!-- ====================================================================== -->

**Ню-мод для Мит** — один самостоятельный плагин BepInEx для
[NeuroMita](https://github.com/VinerX/NeuroMita).

Скачайте его, положите рядом пакет мода, запустите игру. Больше ничего ставить не нужно и
настраивать нечего.

Охвачены пять Мит — те, для которых мод и выпущен: **Crazy / Cappie / Kind / Sleepy / ShortHair**.

---

## Что он делает

Пакеты моделей для этой игры — это контейнеры `UnityFS`, а штатный AssetBundle API в этой сборке
нерабочий: игра никогда не инициализирует подсистему, поэтому все перегрузки
`AssetBundle.LoadFrom*` мертвы. Плагин поэтому **читает контейнер сам** и делает всю работу:

1. **Разбирает пакет** — меши, bind pose, имена костей, материалы и текстуры (декодируются прямо
   из потоков ресурсов контейнера).
2. **Совмещает его со скелетом игры.** Rest pose пакета решается относительно игрового, с проверкой
   невязки: пакет, который не подходит, отклоняется, а не деформируется.
3. **Привязывает** — покость, с bind pose каждой кости, взятым из скелета самой игры, и с починкой
   осиротевших весов вершин.
4. **Скрывает одежду игры**, которая осталась включённой под новой моделью.
5. **Разбирает меш обратно на части и переставляет текстуры.** Об этом ниже — именно это отличает
   «тело со странной шеей» от правильного.

Всё после шага 3 существует из-за того, как автор мода собрал пакет.

---

## Почему шаги 4 и 5 обязательны

### Меш пакета — это три меша

Меш `Body` в пакете — это **три подмеша с тремя разными материалами**:

```
sub0   320 tris   материал 'Cloth'    -> текстура 'Cloth'       ошейник
sub1 30471 tris   материал 'Body_4'   -> текстура 'body_nsfw'   тело
sub2    40 tris   материал 'body'     -> текстура 'Body'        деталь шеи
```

Загрузчик контейнера склеивает их в **один** список треугольников: Unity требует по одному
материалу на подмеш, а материалы частей пакета такой перенос не переживают. Один подмеш может
нести только один материал, поэтому выбранная карта натягивается на все три части сразу — вот
почему наивная установка рисует ошейник кожей, а тело цветом свитера.

При этом ломается в две стороны:

* тело получает **не тот цвет** (выбирается `Cloth`, карта свитера), а
* ошейник и деталь шеи рисуются **чужой картой**, и это видно как резкие клинья ровно на шее,
  потому что UV каждой части разворачивались под свою текстуру.

`MeshSplit` возвращает три части по собственным границам пакета и выдаёт каждой ту карту, под
которую её UV и разворачивали. Меняются только индексы: вершины, веса, bind pose и blend shapes
не трогаются — это важно ещё и потому, что чтение `boneWeights` в рантайме роняет игру.

### Одежда игры всё ещё надета

Пакет закрывает только тело. Рендереры `Sweater` / `Skirt` / `Shoes` / `Pantyhose` самой игры
остаются включёнными и рисуются прямо сквозь него. Скрываются оба написания, потому что игра
использует оба:

```
новый стиль : .../MitaPerson Mita/Slots/SweaterSlot   (и SkirtSlot, ShoesSlot, PantyhoseSlot)
legacy      : .../MitaPerson Mita/Sweater             (и Skirt, Shoes, Pantyhose)
```

### И при этом нельзя тронуть ничего лишнего

Рендерер тела **игрока** тоже называется `Body`. Как и у каждой другой Миты, и как у реквизита в
квестах. Поэтому сопоставление по именам рендереров не работает, а поиск персонажа по имени — тем
более: корень персонажа называется **`Mita Crazy _legacy`**, а не `Mita Crazy`, так что
`GameObject.Find` его не находит.

Каждое изменение ограничено совпадением фрагмента с **полным путём трансформа** — только он это и
различает:

```
GameCore/Gameplay Session/GameController/Player/ViewRoot/Person/Body     <- игрок, не трогаем
MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot          <- обрабатывается
MenuGame/Scene/Mitas/Legacy/Mita Crazy _legacy/MitaPerson Mita/Body     <- обрабатывается
World/Quests/Quest 1/GameCard/MitaGame/MitaPerson Mita/Body             <- реквизит, не трогаем
```

Игра держит каждую Миту сразу в трёх местах (`MenuGame/Scene/Mitas/<имя>`,
`…/Legacy/Mita <имя> _legacy`, `MitaCore (Start)/Mitas/<имя>`), и все три обрабатываются.

---

## Требования

* **BepInEx 6.0.0-be.788** или новее для NeuroMita. Более старые не читают `metadata v39` этой
  игры.
* **Пакет ню-мода.** Это чужая работа, и она намеренно **не** распространяется вместе с плагином.
  Страница мода — **<https://www.nexusmods.com/miside/mods/58>**. Скачайте её и положите в:

  ```
  <игра>\BepInEx\plugins\
  ```

  **Имя файла не важно** — плагин сам находит в этой папке любой контейнер `UnityFS`, у которого
  меш тела совпадает с ожидаемым. Искать можно и в подпапке `NudeMita`, и в корне игры; другое
  место задаётся параметром `General.PackPath`.

---

## Установка

Распакуйте архив в папку игры (ту, где лежит `NeuroMita.exe`) и запустите **`install.bat`**.

Он скопирует четыре файла в `BepInEx\plugins` и скажет, на месте ли пакет. Прав администратора не
нужно, файлы игры не изменяются, повторный запуск безопасен.

## Настройка

`BepInEx\config\neuromita.nudemita.cfg`

| Секция | Ключ | По умолчанию | Значение |
|---|---|---|---|
| General | `Enabled` | `true` | Главный выключатель. |
| General | `PackPath` | `mita_nude` | Где искать пакет. Относительный путь проверяется сначала в `BepInEx\plugins`, затем в папке игры. |
| Diagnostics | `Verbose` | `true` | Что попало в область действия и что изменено. |
| Diagnostics | `DumpScene` | `false` | Инвентаризация всех рендереров сцены, шесть проходов по 15 секунд. |

Это весь файл. Никакой маршрутизации, никаких настроек по персонажам и никакого DSL для пакетов:
плагин существует для одного мода.

### `DumpScene` — самое полезное здесь

Он печатает по каждому рендереру: полный путь, число подмешей и слотов, число костей и bind pose,
имя меша, root bone и его масштаб, мировые границы и альбедо каждого слота материала. Именно так
неправильно привязанный меш опознаётся **без картинки** — у него неверный размер при идеальных
вершинах:

```
[Dump] 'BodySlot' enabled=True subMeshes=3 slots=3 bones=180 bindposes=180 mesh='Body_aligned' … bounds=0.64x1.67x0.62 … path=MitaCore (Start)/Mitas/Mita Dream/…
```

Здоровое тело — примерно `0.7 x 1.7 x 0.7`. Всё около `3 x 3 x 3` — это проблема привязки, а не
модели.

---

## Сборка

Нужен .NET 6 SDK и установленная игра, запущенная хотя бы раз с BepInEx (для `BepInEx\core` и
`BepInEx\interop`).

```powershell
# указать игру одним из способов:
#   -p:GameDir="D:\Games\NeuroMita"  |  $env:NEUROMITA_DIR  |  файл game.dir в корне репозитория

powershell -File package.ps1                                 # сборка + релизный zip
powershell -File package.ps1 -GameDir "D:\Games\NeuroMita"   # сборка + развёртывание
```

`game.dir` намеренно в git-ignore: это локальный путь.

### Оффлайн-проверки

```powershell
powershell -File tools\verify-bindpose-math.ps1   # формула bind pose, без Unity и без игры
```

Скрипт доказывает численно, что исправленный порядок умножения даёт единичную матрицу скиннинга, а
прежний — нет. На теле высотой 1.67 он смещает вершину на 1.8 единицы сцены.

---

## Благодарности и лицензия

* **Пакет нам не принадлежит.** Ню-мод — работа его автора; этот проект только загружает и
  дорабатывает его и не распространяет сам пакет.
* Читатель контейнеров `UnityFS`, решатель совмещения, привязка костей и починка весов происходят
  из **[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models)** (MIT) —
  проекта, который работает с произвольными пакетами и путём FBX. Этот плагин — специфичная для
  ню-мода половина той работы, с исправленной ошибкой bind pose и без частей, нужных только для
  общей установки пакетов.
* Проект под лицензией MIT — см. [LICENSE](LICENSE).

<br>

---

<!-- ====================================================================== -->
<!--  ENGLISH                                                               -->
<!-- ====================================================================== -->

# NeuroMita.NudeMita (English)

**The nude mod for Mita** as one self-contained BepInEx plugin for
[NeuroMita](https://github.com/VinerX/NeuroMita).

Download it, drop the mod pack next to it, launch the game. Nothing else to install, nothing to
configure.

Five Mitas are covered, which is what the mod itself ships for: **Crazy / Cappie / Kind / Sleepy /
ShortHair**.

---

## What it does

Model packs for this game are `UnityFS` containers, and Unity's own AssetBundle API is unusable on
this build — the game never initialises the subsystem, so every `AssetBundle.LoadFrom*` overload is
dead. The plugin therefore **reads the container itself** and does the whole job:

1. **Parses the pack** — meshes, bind poses, bone names, materials, and textures decoded out of the
   container's resource streams.
2. **Aligns it to the game skeleton.** The pack's rest pose is solved onto the game's, with a
   residual check so a pack that does not fit is refused rather than deformed.
3. **Binds it** — bone by bone, each bind pose taken from the game's own skeleton, with orphan
   vertex weights repaired.
4. **Hides the game's clothing slots** that are still enabled underneath.
5. **Splits the mesh back apart and re-maps its textures.** This is what separates "a body with a
   weird neck" from a correct one.

Everything after step 3 exists because of how the pack is authored.

### Why steps 4 and 5 are necessary

The pack's `Body` mesh is **three submeshes with three different materials**:

```
sub0   320 tris   material 'Cloth'    -> texture 'Cloth'       the choker
sub1 30471 tris   material 'Body_4'   -> texture 'body_nsfw'   the body
sub2    40 tris   material 'body'     -> texture 'Body'        the neck piece
```

A container loader has to concatenate those into **one** triangle list, because Unity wants one
material per submesh and the pack's per-part materials do not survive the trip. One submesh can only
carry one material, so whichever map is picked gets painted over all three parts — which is why a
naive install renders the choker in skin, or the body in the sweater's colour. The parts whose UVs
were authored against a different atlas then come out as hard-edged slivers exactly at the neck.

`MeshSplit` puts the three parts back on the pack's own boundaries and gives each the map its UVs
belong to. Only index data is touched: vertices, weights, bind poses and blend shapes are left
alone, which also matters because reading `boneWeights` at runtime crashes this game.

The game's clothing is still on as well: the pack covers only the body, so the game's own
`Sweater` / `Skirt` / `Shoes` / `Pantyhose` renderers would draw straight through it. Both spellings
are hidden, because the game uses both:

```
new-style : .../MitaPerson Mita/Slots/SweaterSlot   (and SkirtSlot, ShoesSlot, PantyhoseSlot)
legacy    : .../MitaPerson Mita/Sweater             (and Skirt, Shoes, Pantyhose)
```

### And it has to leave everything else alone

The **player's** body renderer is literally called `Body`. So is every other Mita's, and so is a
quest prop's. Matching on renderer names therefore cannot work, and neither can looking a character
up by name: the character root is **`Mita Crazy _legacy`**, not `Mita Crazy`, so `GameObject.Find`
misses it and a naive fallback ends up touching the whole scene.

Every change is scoped by matching a fragment against the renderer's **full transform path**, which
is the only thing that tells those apart:

```
GameCore/Gameplay Session/GameController/Player/ViewRoot/Person/Body     <- the player, left alone
MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot          <- covered
MenuGame/Scene/Mitas/Legacy/Mita Crazy _legacy/MitaPerson Mita/Body     <- covered
World/Quests/Quest 1/GameCard/MitaGame/MitaPerson Mita/Body             <- a prop, left alone
```

The game instantiates each Mita in three places at once (`MenuGame/Scene/Mitas/<name>`,
`…/Legacy/Mita <name> _legacy`, `MitaCore (Start)/Mitas/<name>`) and all three are covered.

---

## Requirements

* **BepInEx 6.0.0-be.788** or newer for NeuroMita. Older builds cannot read this game's
  `metadata v39`.
* **The nude mod pack** — **<https://www.nexusmods.com/miside/mods/58>**. It is somebody else's work
  and is deliberately *not* redistributed here. Put the file, **under any name**, into:

  ```
  <game>\BepInEx\plugins\
  ```

  A folder works too, as long as one file inside it is a `UnityFS` container; `General.PackPath`
  points elsewhere if you prefer.

---

## Install

Extract the release zip into the game folder (the one holding `NeuroMita.exe`) and run
**`install.bat`**. It copies four files into `BepInEx\plugins` and tells you whether the pack is in
place. No administrator rights, no game file is ever modified, and running it twice is safe.

## Configuration

`BepInEx\config\neuromita.nudemita.cfg`

| Section | Key | Default | Meaning |
|---|---|---|---|
| General | `Enabled` | `true` | Master switch. |
| General | `PackPath` | `mita_nude` | Where the pack is. Relative paths resolve against `BepInEx\plugins`, then the game folder. |
| Diagnostics | `Verbose` | `true` | Say what came into scope and what changed. |
| Diagnostics | `DumpScene` | `false` | Inventory every renderer in the scene, six times, 15 seconds apart. |

That is the whole file. There is no routing to configure, no per-character setup, and no pack DSL:
this plugin exists for one mod.

### `DumpScene` is the useful one

It prints, per renderer: full transform path, submesh and slot counts, bone and bindpose counts,
mesh name, root bone and its scale, world bounds, and every material slot's albedo. It is how a
wrongly bound mesh gets identified **without looking at a picture** — a mesh bound against the wrong
bones comes out at the wrong size while its vertices are perfectly fine:

```
[Dump] 'BodySlot' enabled=True subMeshes=3 slots=3 bones=180 bindposes=180 mesh='Body_aligned' … bounds=0.64x1.67x0.62 … path=MitaCore (Start)/Mitas/Mita Dream/…
```

A healthy body is about `0.7 x 1.7 x 0.7`. Anything near `3 x 3 x 3` is a binding problem, not a
modelling one.

---

## Building and offline verification

Requires the .NET 6 SDK, and a game install launched once with BepInEx present.

```powershell
# point at a game install, any one of:
#   -p:GameDir="D:\Games\NeuroMita"  |  $env:NEUROMITA_DIR  |  a game.dir file at the repo root

powershell -File package.ps1                                  # build + release zip
powershell -File package.ps1 -GameDir "D:\Games\NeuroMita"     # build + deploy

# neither of these starts the game
powershell -File tools\verify-release.ps1 -Pack <path to the pack>
powershell -File tools\verify-bindpose-math.ps1
```

`tools\verify-release.ps1` runs 49 checks over the built release: version agreement across csproj,
source and assembly; the `BepInPlugin` identity; what the assembly links against; source hygiene;
that every script parses; and the archive's contents, separators, line endings and encodings. Given
`-Pack`, it also re-checks the baked profile against the real container with `tools\bundledump`.

`tools\verify-bindpose-math.ps1` proves the bind-pose formula numerically, with no Unity: the
corrected order reproduces the skinning identity to `1e-16`, while the old order displaces a vertex
by 1.8 scene units on a body 1.67 tall.

---

## Credits and licence

* **The pack is not ours.** The nude mod is the work of its own author; this project only loads and
  finishes it, and does not redistribute it.
* The `UnityFS` container reader, the alignment solver, the bone binder and the weight repair
  descend from **[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models)**
  (MIT), which handles arbitrary packs and the FBX path. This plugin is the nude-mod-specific half
  of that work, with the alignment bind-pose bug fixed and the parts that only matter to general
  pack installation left out.
* This project is MIT — see [LICENSE](LICENSE).

<br>

---
<!-- ====================================================================== -->
<!--  中文（次要语言）                                                       -->
<!-- ====================================================================== -->

# NeuroMita.NudeMita（中文）

**米塔裸体 mod** —— 一个自包含的 NeuroMita BepInEx 插件。

下载它，把 mod 包放在旁边，启动游戏。不需要再装别的东西，也没有需要配置的地方。

覆盖 mod 本身声明支持的五个米塔：**疯狂 / 帽子 / 善良 / 瞌睡 / 短发**。

## 它做什么

本游戏的模型包是 `UnityFS` 容器，而这套构建里 Unity 自带的 AssetBundle API 是**不可用的**
（游戏从不初始化该子系统，所有 `AssetBundle.LoadFrom*` 都是死的）。所以插件**自己解析容器**，
并把整件事做完：

1. **解析包** —— 网格、bind pose、骨骼名、材质，以及贴图（直接从容器的资源流里解码）。
2. **与游戏骨架对齐** —— 用残差门槛判定，装不上的包会被明确拒绝，而不是被拉变形。
3. **绑定** —— 逐骨骼绑定，每根骨骼的 bind pose 取自游戏自己的骨架，并修复孤立顶点权重。
4. **隐藏游戏自己的衣服槽位**（它们还在新模型下面渲染）。
5. **把网格拆回原来的部件，并重新指定贴图** —— 这一步决定了成品是"脖子奇怪的裸体"还是正确的裸体。

### 为什么第 4、5 步必须做

包里 `Body` 网格是**三个子网格、三个不同材质**：

```
sub0   320 三角形   材质 'Cloth'    -> 贴图 'Cloth'        项圈
sub1 30471 三角形   材质 'Body_4'   -> 贴图 'body_nsfw'    身体
sub2    40 三角形   材质 'body'     -> 贴图 'Body'         颈部件
```

容器加载器必须把它们拼成**一个**三角形列表（Unity 要求一个子网格一个材质，而包的各部件材质
无法在搬运中保留）。一个子网格只能有一个材质，被选中的那张图会盖住全部三个部件 —— 这就是为什么
简单装上会出现"项圈变皮肤色"或"身体变毛衣色"。

而且两个方向的错误同时存在：身体颜色错（选了 `Cloth`，毛衣的图），项圈和颈部件用了**别人的图集**
（各自 UV 是按自己的贴图展开的），表现为**脖子上那圈硬边三角**。

`MeshSplit` 按包自己的边界把三部分拆回去，并给每部分它 UV 所针对的那张贴图。只改索引数据：
顶点、权重、bind pose、blend shape 都不动 —— 这也重要，因为运行时读 `boneWeights` 会让游戏崩溃。

另外，**玩家的身体渲染器也叫 `Body`**，每个米塔的都是，任务道具也是；而角色根叫
`Mita Crazy _legacy` 而不是 `Mita Crazy`，所以 `GameObject.Find` 找不到它。所有改动都按**完整
变换路径**限定作用域，这是唯一能区分它们的办法。

## 要求

* **BepInEx 6.0.0-be.788 或更新**（更老的读不了本游戏的 `metadata v39`）。
* **裸体 mod 包本身**。它是别人的作品，因此**不随本插件分发**。mod 主页：
  **<https://www.nexusmods.com/miside/mods/58>**。下载后放到：

  ```
  <游戏>\BepInEx\plugins\
  ```

  **文件名随意** —— 插件会自己在该目录下寻找任何 `UnityFS` 容器，并用"身体网格是否匹配"来校验。
  放在 `NudeMita` 子目录或游戏根目录也可以；其他位置用 `General.PackPath` 指定。

## 安装

把发布压缩包解压到游戏目录（含 `NeuroMita.exe` 那层），双击 **`install.bat`**。它会把四个文件
放进 `BepInEx\plugins`，并告诉你包是否就位。不需要管理员权限，不改动任何游戏文件，重复运行安全。

## 配置

`BepInEx\config\neuromita.nudemita.cfg` 只有四个选项：`Enabled`、`PackPath`、`Verbose`、
`DumpScene`。没有路由、没有按角色的设置、没有包 DSL —— 这个插件只为这一个 mod 存在。

`DumpScene` 是最有用的一个：它打印每个渲染器的完整路径、子网格/槽位/骨骼/bindpose 数量、网格名、
root bone 与缩放、世界包围盒、以及每个材质槽的 albedo。这是**不用看图**就能定位绑定错误的方法 ——
顶点没问题但尺寸不对，就是绑定问题：

```
[Dump] 'BodySlot' … bounds=0.64x1.67x0.62 … path=MitaCore (Start)/Mitas/Mita Dream/…
```

健康身体约 `0.7 x 1.7 x 0.7`；接近 `3 x 3 x 3` 就是绑定错误，不是模型问题。

## 构建与离线验证

```powershell
powershell -File package.ps1                                 # 构建 + 出包
powershell -File tools\verify-bindpose-math.ps1              # bind pose 公式的纯数学验证（不需要游戏）
```

## 致谢与许可

* **包不是我们的。** 裸体 mod 是其作者的作品；本项目只负责加载与收尾，不分发该包。
* `UnityFS` 解析器、对齐求解、骨骼绑定、权重修复来自
  **[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models)**（MIT）。
  本插件是那项工作中"裸体 mod 专用"的一半，修掉了 bind pose 的错误，并去掉了只有通用装包才需要的部分。
* 本项目为 MIT 许可 —— 见 [LICENSE](LICENSE)。
