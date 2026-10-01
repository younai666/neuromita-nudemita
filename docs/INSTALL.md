# Установка / 安装 — NeuroMita.NudeMita

<!-- ============================ РУССКИЙ ============================ -->

## Что нужно

1. **BepInEx 6.0.0-be.788** или новее, установленный в игру. Более старые сборки не читают
   `metadata v39` этой игры, и без BepInEx здесь ничего не работает.
2. **Пакет ню-мода.** Этот плагин — загрузчик и доработка; сама модель это пакет. Это чужая
   работа, и вместе с плагином она не поставляется. Страница мода:
   **<https://www.nexusmods.com/miside/mods/58>**

## Установка за четыре шага

**Шаг 1.** Скачайте пакет ню-мода со страницы мода в Nexus Mods —
**<https://www.nexusmods.com/miside/mods/58>** — и положите файл в:

```
<игра>\BepInEx\plugins\
```

**Имя файла не важно.** Плагин сам найдёт в этой папке любой контейнер `UnityFS`, у которого меш
тела совпадает с ожидаемым. Можно положить и в подпапку `NudeMita`, и в корень игры.

**Шаг 2.** Распакуйте архив этого плагина в папку игры — ту, где лежит `NeuroMita.exe`.

**Шаг 3.** Запустите **`install.bat`** из распакованной папки. Он:

* найдёт игру (или примет папку игры, перетащенную на него),
* скопирует `NeuroMita.NudeMita.dll` и три нужные ему сборки в `BepInEx\plugins`,
* скажет, на месте ли пакет.

**Шаг 4.** Запустите игру.

Прав администратора не нужно, файлы игры не изменяются, повторный запуск безопасен.

### Вручную

Скопируйте эти четыре файла из `BepInEx\plugins` архива в `BepInEx\plugins` игры:

```
NeuroMita.NudeMita.dll
AssetsTools.NET.dll
AssetsTools.NET.Texture.dll
AssetRipper.TextureDecoder.dll
```

Больше ничего. Нативной библиотеки нет.

## Что должно быть в логе

```
[Nude] ===== NeuroMita.NudeMita 0.1.0-pre1 =====
[Nude] pack ready: mita_nude (3 part(s), 3 texture(s))
[Nude]   part 'Body' (17824 verts, 180 bones)
[Nude] 81/135 renderer(s) in scope for 7 character fragment(s)
[Nude] MenuGame/Scene/Mitas/Mita Crazy/MitaPerson Mita/Slots/BodySlot: part='Body' ok=True bones=180 missing=5 align=[Right toe/Head/Right Eye] residual=0.0001
[Nude] hid 'SweaterSlot' at MenuGame/…/Slots/SweaterSlot
[Nude] split 'BodySlot' into 3 part(s) [320, 30471, 40] tris at MenuGame/…/Slots/BodySlot
```

Устанавливаются и разбираются **пятнадцать** рендереров: пять Мит, каждая из которых присутствует
в игре в трёх местах сразу.

## Чтение лога

| Строка | Что значит |
|---|---|
| `the nude mod pack was not found` | Положите пакет, как в шаге 1, или укажите `General.PackPath`. Поиск повторяется каждые несколько секунд — **перезапускать игру не нужно**. |
| `pack ready: 3 part(s), 3 texture(s)` | Контейнер прочитан. |
| `ok=True … residual=0.0001` | Rest pose пакета совмещён с этим персонажем. `residual` заметно выше `0.02` означает, что пакет собран на другом риге и отклонён. |
| `split 'BodySlot' into 3 part(s)` | Границы подмешей пакета восстановлены. |
| `has N triangles, not the pack's 30831` | Этот рендерер ещё не несёт тело пакета. До установки это нормально; повторяется каждые 2 секунды. |
| `T/N renderer(s) in scope` | Сколько сцены плагину разрешено трогать. `0/N` — что-то не так; почти `N` — фрагменты персонажей совпали со всем подряд. |

## Если тело выглядит растянутым или разорванным

Включите `Diagnostics.DumpScene = true` и найдите в логе нужного персонажа:

```
[Dump] 'BodySlot' enabled=True subMeshes=3 slots=3 bones=180 bindposes=180 mesh='Body_aligned' … bounds=0.64x1.67x0.62 … path=MitaCore (Start)/Mitas/Mita Dream/…
```

Здоровое тело — примерно `0.7 x 1.7 x 0.7` при масштабе `1.000`. Границы около `3 x 3 x 3`
означают, что меш привязан не к тем костям: это проблема привязки, а не модели, и эти значения стоит
приложить к отчёту.

## Удаление

Удалите из `BepInEx\plugins` файл `NeuroMita.NudeMita.dll` и три сборки `AssetsTools*` /
`AssetRipper*`, а также, при желании, `BepInEx\config\neuromita.nudemita.cfg`. Пакет можно оставить.
Ничего на диске не изменяется, файлы игры не затрагиваются.

<br>

---

<!-- ============================ 中文 ============================ -->

## 需要什么

1. **BepInEx 6.0.0-be.788 或更新**，已装进游戏。更老的构建读不了本游戏的 `metadata v39`。
2. **裸体 mod 包本身**。本插件是加载器与收尾工作，模型本体是那个包 —— 它是别人的作品，不随插件分发。
   mod 主页：**<https://www.nexusmods.com/miside/mods/58>**

## 四步安装

**第 1 步**：从 Nexus Mods 的 mod 主页（**<https://www.nexusmods.com/miside/mods/58>**）下载裸体
mod 包，把文件放进：

```
<游戏>\BepInEx\plugins\
```

**文件名随意** —— 插件会自己在该目录下找任何 `UnityFS` 容器，并用"身体网格是否匹配"来校验。
放进 `NudeMita` 子目录或游戏根目录也可以。

**第 2 步**：把本插件的压缩包解压到游戏目录（含 `NeuroMita.exe` 那层）。

**第 3 步**：双击解压出来的 **`install.bat`**。它会找到游戏、把 `NeuroMita.NudeMita.dll` 和三个
依赖放进 `BepInEx\plugins`，并告诉你包是否就位。

**第 4 步**：启动游戏。

不需要管理员权限，不改动游戏文件，重复运行安全。

### 手动安装

把压缩包里 `BepInEx\plugins` 下的四个文件复制到游戏的 `BepInEx\plugins`：

```
NeuroMita.NudeMita.dll
AssetsTools.NET.dll
AssetsTools.NET.Texture.dll
AssetRipper.TextureDecoder.dll
```

没有原生库。

## 日志里应该看到什么

```
[Nude] pack ready: mita_nude (3 part(s), 3 texture(s))
[Nude]   part 'Body' (17824 verts, 180 bones)
[Nude] 81/135 renderer(s) in scope for 7 character fragment(s)
[Nude] …/Slots/BodySlot: part='Body' ok=True bones=180 missing=5 residual=0.0001
[Nude] hid 'SweaterSlot' …
[Nude] split 'BodySlot' into 3 part(s) [320, 30471, 40] tris
```

会安装并拆分**十五个**渲染器：五个米塔，每个在游戏里同时存在三份。

| 日志行 | 含义 |
|---|---|
| `the nude mod pack was not found` | 按第 1 步放好包，或设置 `General.PackPath`。搜索每几秒重试一次，**不需要重启游戏**。 |
| `pack ready: 3 part(s), 3 texture(s)` | 容器读取成功。 |
| `ok=True … residual=0.0001` | 包的对齐已解到该角色。`residual` 明显大于 `0.02` 表示包是用别的骨架做的，会被拒绝。 |
| `split 'BodySlot' into 3 part(s)` | 包的子网格边界已恢复。 |
| `has N triangles, not the pack's 30831` | 该渲染器还没有装上包的身体。安装落地前这是正常的，每 2 秒重试。 |
| `T/N renderer(s) in scope` | 插件被允许触碰的场景比例。`0/N` 说明有问题；接近 `N` 说明角色片段匹配到了所有东西。 |

## 如果身体看起来被拉伸或撕裂

打开 `Diagnostics.DumpScene = true`，在日志里找对应角色：

```
[Dump] 'BodySlot' … bounds=0.64x1.67x0.62 … path=MitaCore (Start)/Mitas/Mita Dream/…
```

健康身体约 `0.7 x 1.7 x 0.7`，缩放 `1.000`。包围盒接近 `3 x 3 x 3` 说明网格绑到了错误的骨骼上 ——
这是绑定问题而非模型问题，请把这些数值一起反馈。

## 卸载

从 `BepInEx\plugins` 删掉 `NeuroMita.NudeMita.dll` 和三个 `AssetsTools*` / `AssetRipper*` 程序集，
可选再删 `BepInEx\config\neuromita.nudemita.cfg`。包可以留着。磁盘上不改动任何东西。
