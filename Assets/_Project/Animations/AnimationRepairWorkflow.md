# Unagi 动画修复与导入规范

这份规范记录 `Attack_01` 修复过程中的结论。后续动画必须逐个处理、逐个验收，不能把一次批量导出当成批量替换授权。

## 当前明确的问题范围

用户已确认以下 9 个动作存在手部或武器问题，后续只修复这一组：

```text
Unagi_Attack_02.fbx
Unagi_Attack_04.fbx
Unagi_Attack_04_02.fbx
Unagi_Attack_05.fbx
Unagi_Attack_05_02.fbx
Unagi_Attack_06.fbx
Unagi_Attack_Branch_01_Attack_01.fbx
Unagi_Attack_Branch_02_Attack_01.fbx
Unagi_Attack_Rush.fbx
```

对应项目目录：

`Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/01_Attack/`

这 9 个动作之外的动画不做自动筛查、不做批量导出、不覆盖 FBX，也不修改 Animator 或 Ability 引用。每个动作都要单独修复、单独导入、单独由用户在 Unity 中验收后，才处理列表中的下一个。

## 在用动画的独立片段迁移（2026-09-26）

用户随后明确要求将项目中其他**正在使用**的 FBX 动画也迁移为独立 `.anim`。这项授权扩展的是在用片段的格式和引用迁移；上面的 9 个问题动作仍是手部与武器修复范围，不能据此批量修改其余动作姿势或覆盖原始 FBX。

`Player_Swordman.controller` 中原本还引用 11 个 FBX 内嵌片段：`Unagi_Attack_01_End`、`Unagi_Attack_02_End`、`Unagi_Attack_03`、`Unagi_Attack_03_End`、`Unagi_Attack_05_End`、`Unagi_Attack_06_End`、`Unagi_Idle`、`Unagi_Run`、`Unagi_Run_Start`、`Unagi_Run_End`、`Unagi_Walk`。这些片段已在原 FBX 相邻目录生成同名 `.anim`，相关 Animator 状态与 6 个 Ability 已切换到独立片段。另一个使用 `Unagi_Attack_04` 的沙盒 Ability 也已切换到已修复的 `.anim`。

迁移只复制动画曲线，不修这些 11 个片段的动作。导入后已用 `Unagi_Idle.fbx` 模型比较全部整帧与半帧姿势、曲线、时长和事件；原 FBX 保留，模型、网格、Avatar 和导入缩放不变。迁移前备份与验证报告位于 `Library/UnagiBatchImport/UsedFbxBeforeMigration`、`used_clip_migration_report.txt` 和 `used_clip_validation_report.txt`。其余未被引用的 FBX 不属于此次迁移范围。

## 整个 Unagi 目录的片段迁移（2026-09-26）

用户进一步明确要求把 `Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/` 下所有动画迁移为独立 `.anim`。该目录共有 70 个 FBX，其中 21 个此前已有对应 `.anim`；本次为其余 49 个创建同名独立片段，保留子目录结构。每个新片段导入后均比较了原 FBX 的曲线、帧率、时长、动画事件，以及使用 `Unagi_Idle.fbx` 模型采样的全部整帧和半帧姿势。结果为 49 个通过、0 个失败，详见 `Library/UnagiBatchImport/all_unagi_migration_report.csv`。

此次为未使用的 FBX 动画创建片段，不额外修改动作姿势或 Animator/Ability 引用。此前已迁移的在用片段及已修复的动作 `.anim` 保持原样。49 份源 FBX 和 `.meta` 的备份保存在 `Library/UnagiBatchImport/AllUnagiBeforeMigration`；项目内原始 FBX 仍保留，尤其 `Unagi_Idle.fbx` 继续提供模型、骨架和 Avatar。

## 迁移后的源 FBX 清理（2026-09-26）

用户进一步授权删除原本不再使用的文件。检查确认 70 个源 FBX 中，只有 `02_Idle/Unagi_Idle.fbx` 的 GUID 仍被场景引用，继续作为角色模型、骨架和 Avatar。其余 69 个 FBX 均有对应 `.anim`，且没有场景、预制体、Animator 或 Ability 的序列化引用；它们及各自 `.meta` 已从 `Assets` 删除。删除前全部备份并逐个核对哈希，备份位于 `Library/UnagiBatchImport/RemovedFbxBackup`。项目内现在有 70 个独立 `.anim` 和 1 个模型 FBX。

早期一次性编辑器修复脚本仍记录了源 FBX 的旧路径；如需重跑这些历史修复流程，须先从备份恢复相应 FBX。当前运行时动画和模型不依赖这些旧路径。

## 独立片段的 Inspector 预览模型（2026-09-26）

Unity 的 `.anim` 不单独保存预览模型引用。选择此目录下任意 Unagi `.anim` 时，编辑器脚本 `UnagiAnimationPreviewModel` 会把 Generic 动画预览模型设为现有的 `02_Idle/Unagi_Idle.fbx`，使 Inspector 底部的预览区显示同一个角色及武器。Unity 重新编译脚本或重启后，该脚本会在再次选中 Unagi 片段时重新设置预览模型；也可执行 `Tools > Unagi > Bind Preview Model and Validate Clips` 手动检查 70 个片段的 Rig 类型和模型骨骼路径。该设置只影响编辑器预览，不修改动画曲线、Animator、Ability 或运行时模型。

## 资源职责

外部 3ds Max 主版源文件目录是：

`D:\BaiduNetdiskDownload\039-雅\max\雅\`

这里只处理该目录下的主版 `.max` 文件；`D:\BaiduNetdiskDownload\039-雅\max\雅\MAX\` 是变体目录，除非明确指定，否则不参与主版动画修复。外部源文件只读使用，不能在原目录直接保存或覆盖。

项目内的临时工作目录是：

`D:\19356\UnityLearning\Library\UnagiBatchImport\`

其中可以保存复制后的 `.max`、候选 FBX、导出脚本、备份和验证报告。这个目录属于 Unity 缓存/工作区，不是运行时资源目录；其中的候选 FBX 不得直接被 Animator 或 Ability 引用。

运行时只需要一份角色模型 FBX。当前模型基准是：

`Assets/ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/02_Idle/Unagi_Idle.fbx`

这份 FBX 提供角色网格、骨架、SkinnedMeshRenderer 和 Avatar。动作文件只保存骨骼动画曲线，统一使用独立 `.anim`。动作 `.anim` 的骨骼路径必须和模型 FBX 完全一致。

重新导出的动作 FBX 只能放在 `Library/UnagiBatchImport` 等临时目录中用于分析和生成 `.anim`，不能直接覆盖正在使用的模型 FBX，也不能让 Animator 使用它的内嵌 AnimationClip。

## 项目目录

推荐的运行时布局：

```text
Assets/
├─ ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/02_Idle/
│  └─ Unagi_Idle.fbx                 # 唯一运行时模型
├─ ThirdParty/UnagiAnimationPack/FBX/Main/Unagi/01_Attack/
│  ├─ Unagi_Attack_01.anim            # 独立动作片段
│  ├─ Unagi_Attack_02.anim
│  └─ ...
├─ _Project/Animations/Controllers/
│  └─ Player_Swordman.controller
└─ _Project/CombatEditor/ScriptableObjects/Abilities/
   └─ ...                             # Ability 的 Clip 指向同一个 .anim
```

同一动作不要同时保留一个运行时 FBX 动画、一个独立 `.anim` 和一个 `Fixed.anim` 让引用关系变得不明确。源 `.max`、候选 FBX、备份和检查报告放在 `Library/UnagiBatchImport`，不作为运行时资源。

## 单个动画的修复流程

1. 只接收一个明确的异常，例如 `Attack_02` 手部错位。先不处理其他动作。
2. 备份该动作当前的 FBX、`.meta`、Animator 引用和 Ability 引用。确认旧动画的 GUID、片段时长、帧率和结束帧。
3. 在 3ds Max 原始场景中逐帧检查右手、刀柄和武器相关骨骼。记录异常帧以及前后正常帧；不要修改下载目录的原始 `.max`。
4. 只修异常关键帧。优先沿用 `Attack_01` 的方式：从原动画生成独立 `.anim`，在异常帧修改刀柄或手部曲线，相邻帧保持不变。
5. 使用唯一模型 FBX `Unagi_Idle.fbx` 在 Unity 中采样检查：骨骼路径、模型层级、动画时长、帧率、异常帧、前后帧和帧间插值。
6. 验证通过后，把 `.anim` 放到对应动作目录，例如 `01_Attack/Unagi_Attack_02.anim`，让 Animator 状态和 Ability 的 `Clip` 都引用该 `.anim` 的 GUID。
7. 只刷新并验收这一个动作。由用户在 Unity 中播放确认后，才开始下一个动作。

## 验收标准

- Animator 状态名称和 Ability 名称保持原样。
- Animator 与 Ability 指向同一个独立 `.anim`，不指向动作 FBX 的内嵌 AnimationClip。
- `.anim` 的所有骨骼路径都能在唯一模型 FBX 中找到。
- 动画长度、帧率、起止帧与原动作一致；修复动作不能被截短或变速。
- 异常帧的手部、刀柄和武器相对位置连续；至少检查前一帧、异常帧、后一帧以及半帧插值。
- 运行场景中角色模型、网格、材质、Avatar 和预制体引用没有丢失。
- 用户在 Unity 中直接播放并确认后，才算完成。

## 单位和人物尺寸

导入动作时必须以现有模型的单位为基准。不要为了修动作改变模型的缩放、Root 缩放或场景缩放。

- 记录模型 FBX 当前 `ModelImporter.globalScale`、模型身高和根节点缩放，导入前后比较。
- 动作 `.anim` 不应包含改变角色整体尺寸的缩放曲线；检查 Root 和骨架的 `localScale` 是否保持原值。
- 3ds Max 导出时使用与原模型相同的单位和 FBX 单位设置；不要把一次导出的 `cm`、`m` 转换混入动作修复。
- Unity 中模型的 `globalScale`、Rig 类型、Avatar 配置和 `motionNodeName` 以原模型导入设置为准，不要复制候选 FBX 的网格或缩放设置覆盖它们。
- 用 Unity 场景中的同一个模型实例比较导入前后的身高、脚底位置、根节点位置和武器尺寸。尺寸变化时立即停止，不要继续改 Animator。

## 必须避免

- 不要一次性覆盖 70 个旧 FBX。
- 不要修改已经验收正常的 `Attack_01`。
- 不要把重新导出的整份 FBX 当作独立 `.anim` 的等价替代。
- 不要因为骨骼路径数量相同就认定动作播放一定相同；还要比较曲线属性、旋转表示、帧间插值和实际场景播放。
- 不要在没有验证的情况下把 Animator 从已通过验收的 `.anim` 改回 FBX 内嵌动画。
- 不要删除旧资源或修改 `.meta` GUID，除非先备份并明确确认引用已经迁移。
- 不要把临时候选 FBX、重复的 `ReexportCheck` 文件或多个 `Fixed.anim` 留在运行时目录中。

`Attack_01` 的当前基线是：独立 `.anim` 在 Unity 中第 3 帧刀柄到右手距离约 `0.054045 m`，原始异常值约 `0.108856 m`；这个已验收片段只能作为参考基线，不能用未经验证的 FBX 覆盖它。
