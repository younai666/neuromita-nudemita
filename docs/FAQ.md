# FAQ — NeuroMita.NudeMita

<!-- ============================ РУССКИЙ ============================ -->

### Где сама модель? Я получил только плагин.

Модель — это **пакет**, и он чужая работа, поэтому вместе с плагином не поставляется. Скачайте его
со страницы мода — **<https://www.nexusmods.com/miside/mods/58>** — и положите файл в
`<игра>\BepInEx\plugins\`. Имя файла любое. Плагин напишет в лог, если не найдёт его.

### Нужен ли ещё NeuroMita.CustomModels?

Нет, и ставить оба не нужно. Этот плагин читает пакет сам — эта половина в него встроена — а два
плагина, заменяющих одно и то же тело, будут драться за него.

### Какие Митьи охвачены?

Пять, для которых выпущен мод: **Crazy, Cappie, Kind, Sleepy, ShortHair**. Каждая из них существует
в игре в трёх экземплярах (меню, legacy и `MitaCore (Start)`), и все три обрабатываются.

Mila и Ghost намеренно не тронуты: они не входят в набор, под который собран пакет, и установка
тела на них даёт меш, не совпадающий с их пропорциями.

### Игрок тоже голый. Это был баг?

Да, в ранней сборке, и это исправлено.

Рендерер тела **игрока** называется `Body` ровно так же, как у Миты, и так же называется реквизит в
квестах. Сопоставление по именам рендереров их не различает, а `GameObject.Find` — тем более: корень
персонажа называется `Mita Crazy _legacy`, а не `Mita Crazy`, поэтому поиск по имени его не находит, и
наивный запасной путь в итоге трогает всю сцену.

Теперь область действия определяется совпадением фрагмента с **полным путём трансформа**:

```
GameCore/Gameplay Session/GameController/Player/ViewRoot/Person/Body   <- игрок, не трогаем
MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot        <- обрабатывается
World/Quests/Quest 1/GameCard/MitaGame/MitaPerson Mita/Body           <- реквизит, не трогаем
```

### На шее жёсткие клинья, или ошейник цвета кожи.

Это проблема склеенных подмешей. Меш `Body` в пакете — три подмеша с тремя разными материалами:

```
sub0   320 tris   материал 'Cloth'    -> текстура 'Cloth'       ошейник
sub1 30471 tris   материал 'Body_4'   -> текстура 'body_nsfw'   тело
sub2    40 tris   материал 'body'     -> текстура 'Body'        деталь шеи
```

Загрузчик контейнера обязан склеить их в один подмеш, а один подмеш несёт только один материал —
поэтому выбранная карта натягивается на все три части, и части, чьи UV разворачивались под другую
карту, выходят клиньями.

Плагин возвращает три части по собственным границам пакета и выдаёт каждой ту карту, под которую её
UV и разворачивали. Если клинья остались — у вас другой пакет: включите `Diagnostics.DumpScene = true`
и пришлите из лога значения `mesh='…'` и число костей.

### Тело выглядит растянутым или разорванным, а не просто не того цвета.

Это проблема привязки, а не модели, и она диагностируется без картинки. Включите
`Diagnostics.DumpScene` и сравните:

```
здоровое : subMeshes=3 slots=3 bones=180 bindposes=180 bounds=0.7 x 1.7 x 0.7   scale=1.000
разорвано: …                                            bounds=3.2 x 3.2 x 3.2
```

Если тело намного больше головы, а голова нормальная — меш скинится костями, под которые его не
собирали. Приложите к отчёту ещё `align=[…]` и `residual=` из строки `part='Body'`.

### `texture 'body_nsfw' is not in the pack`

Плагин не нашёл эту карту в собственной таблице текстур пакета и откатился на то, что загружено в
сцене. Скорее всего пакет пересобран. `Diagnostics.DumpScene` печатает реально используемые текстуры.

### Ничего не происходит, в логе `0/N renderer(s) in scope`.

Ни один персонаж не совпал. Обычно это значит, что пакет на месте, но ни одна из пяти Мит ещё не
загружена. Плагин пересканирует сцену каждые 2 секунды, так что достаточно войти в сцену с ней.

### Можно ли использовать с другим пакетом?

Нет. Границы подмешей, соответствие текстур и список из пяти персонажей — свойства именно этого мода,
и они вшиты намеренно. Для произвольных пакетов есть
[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models).

<br>

---
<!-- ============================ ENGLISH ============================ -->

# FAQ (English)

### Where is the model? I only got a plugin.

The model is the **pack**, and it is somebody else's work, so it is not redistributed with this
plugin. Download it from the mod page — **<https://www.nexusmods.com/miside/mods/58>** — and put the
file into `<game>\BepInEx\plugins\`. The file name does not matter. The plugin says so in the log if
it cannot find it.

### Do I need NeuroMita.CustomModels as well?

No, and you should not install both. This plugin reads the pack itself — that half is built in — and
two plugins replacing the same body would fight over it.

### Which Mitas does it cover?

The five the mod ships for: **Crazy, Cappie, Kind, Sleepy, ShortHair**. Each of those exists three
times in the game (a menu copy, a legacy copy and a `MitaCore (Start)` copy) and all three are
handled.

Mila and Ghost are deliberately untouched — they are not in the set the pack was authored for, and
installing the body on them produces a mesh that does not match their proportions.

### The player is nude too. Was that a bug?

Yes, in an earlier build, and it is fixed.

The player's body renderer is literally named `Body`, exactly like a Mita's, and so is a quest
prop's. Matching on renderer names cannot tell them apart, and neither can `GameObject.Find`: the
game calls the character root `Mita Crazy _legacy`, not `Mita Crazy`, so a name lookup misses it
entirely and a naive fallback ends up touching the whole scene.

Scope is now decided by matching a fragment against each renderer's **full transform path**:

```
GameCore/Gameplay Session/GameController/Player/ViewRoot/Person/Body   <- the player, left alone
MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot        <- covered
World/Quests/Quest 1/GameCard/MitaGame/MitaPerson Mita/Body           <- a prop, left alone
```

### The neck has hard-edged slivers, or the choker is skin-coloured.

That is the welded-submesh problem. The pack's `Body` mesh is three submeshes with three different
materials:

```
sub0   320 tris   material 'Cloth'    -> texture 'Cloth'       the choker
sub1 30471 tris   material 'Body_4'   -> texture 'body_nsfw'   the body
sub2    40 tris   material 'body'     -> texture 'Body'        the neck piece
```

A container loader has to concatenate them into one submesh, and one submesh can only carry one
material — so whichever map is picked gets painted over all three parts, and the parts whose UVs
were authored against a different atlas come out as slivers.

This plugin puts the three parts back using the pack's own triangle counts and gives each part the
map its UVs belong to. If you still see slivers, the pack is not the one this was written against:
run with `Diagnostics.DumpScene = true` and report the `mesh='…'` and bone counts from the log.

### The body looks stretched or torn, not just wrong-coloured.

That is a binding problem, not a modelling one, and it is worth reporting because it is diagnosable
without seeing the screen. Turn on `Diagnostics.DumpScene`, find the character, and compare:

```
healthy : subMeshes=3 slots=3 bones=180 bindposes=180 bounds=0.7 x 1.7 x 0.7   scale=1.000
torn    : …                                            bounds=3.2 x 3.2 x 3.2
```

The body being far larger than the head while the head is normal size means the mesh is being
skinned against bones it was not authored for. Include the `align=[…]` and `residual=` values from
the `part='Body'` line as well.

### `texture 'body_nsfw' is not in the pack`

The plugin could not find that map in the pack's own texture table and fell back to whatever is
loaded in the scene. The pack has probably been re-authored; `Diagnostics.DumpScene` prints the
textures actually in use.

### Nothing happens at all, and the log says `0/N renderer(s) in scope`.

No character matched. That usually means the pack is in place but none of the five Mitas is loaded
yet — the plugin rescans every 2 seconds, so entering a scene with one is enough.

### Does it modify game files?

No. Nothing on disk is touched, and uninstalling is deleting four DLLs and optionally one config
file.

### Can I use it with a different pack?

No. The submesh boundaries, the texture mapping and the five target characters are all properties of
this one mod, and they are baked in on purpose. For arbitrary packs use
[NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models).

<br>

---

<!-- ============================ 中文 ============================ -->

### 模型本体在哪？我只拿到一个插件。

模型本体是**那个包**，它是别人的作品，因此不随插件分发。请从 mod 主页
（**<https://www.nexusmods.com/miside/mods/58>**）下载，把文件放进 `<游戏>\BepInEx\plugins\`，
文件名随意。插件找不到时会在日志里说明。

### 还需要 NeuroMita.CustomModels 吗？

不需要，而且不应该两个都装。本插件自己解析包（那一半已内置）；两个插件替换同一个身体会互相打架。

### 覆盖哪些米塔？

mod 本身声明支持的那五个：**疯狂 / 帽子 / 善良 / 瞌睡 / 短发**。每个在游戏里同时存在三份
（菜单、legacy、`MitaCore (Start)`），三份都会处理。

米拉和幽灵米塔是**刻意不动**的：它们不在包所针对的集合里，装上去会得到一个与该角色比例不匹配的网格。

### 玩家也变裸体了，这是 bug 吗？

是早期版本的问题，已修复。

**玩家**的身体渲染器名字就叫 `Body`，和米塔一模一样，任务道具也是。按渲染器名字匹配无法区分，
而 `GameObject.Find` 更不行：角色根叫 `Mita Crazy _legacy` 而不是 `Mita Crazy`，按名字查找根本
找不到，于是幼稚的兜底逻辑就碰到了整个场景。

现在作用域由**完整变换路径**的片段匹配决定：

```
GameCore/Gameplay Session/GameController/Player/ViewRoot/Person/Body   ← 玩家，不动
MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot        ← 处理
World/Quests/Quest 1/GameCard/MitaGame/MitaPerson Mita/Body           ← 任务道具，不动
```

### 脖子上有硬边三角，或者项圈变成皮肤色。

这是子网格被焊死的问题。包里 `Body` 网格是三个子网格、三个不同材质：

```
sub0   320 三角形   材质 'Cloth'    → 贴图 'Cloth'        项圈
sub1 30471 三角形   材质 'Body_4'   → 贴图 'body_nsfw'    身体
sub2    40 三角形   材质 'body'     → 贴图 'Body'         颈部件
```

容器加载器必须把它们拼成一个子网格，而一个子网格只能有一个材质 —— 所以被选中的那张图会盖住
全部三个部件，那些 UV 是按另一张图展开的部件就表现为硬边三角。

插件按包自己的边界把三部分拆回去，并给每部分它 UV 所针对的贴图。如果尖刺仍在，说明你的包不是
本插件针对的那一个：打开 `Diagnostics.DumpScene = true`，把日志里的 `mesh='…'` 和骨骼数发来。

### 身体被拉伸或撕裂，而不是单纯颜色不对。

这是绑定问题而非模型问题，而且不需要看图就能诊断。打开 `Diagnostics.DumpScene` 对比：

```
健康  : subMeshes=3 slots=3 bones=180 bindposes=180 bounds=0.7 x 1.7 x 0.7   scale=1.000
撕裂  : …                                            bounds=3.2 x 3.2 x 3.2
```

身体远大于头部、而头部正常，说明网格被绑到了不属于它的骨骼上。请把 `part='Body'` 那行的
`align=[…]` 和 `residual=` 一并附上。

### 日志说 `texture 'body_nsfw' is not in the pack`

插件在包自己的贴图表里没找到这张图，退回使用场景里已加载的。多半是包被重新打包过。
`Diagnostics.DumpScene` 会打印实际在用的贴图名。

### 什么都没发生，日志是 `0/N renderer(s) in scope`。

没有角色匹配上。通常意味着包已就位，但五个米塔还没加载。插件每 2 秒重新扫描，进入有米塔的场景即可。

### 能用于其他包吗？

不能。子网格边界、贴图对应关系和那五个角色都是这一个 mod 的属性，是**刻意写死**的。
通用包请用 [NeuroMita.CustomModels](https://github.com/younai666/neuromita-custom-models)。
