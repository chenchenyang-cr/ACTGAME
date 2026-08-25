# Unity 资源目录约定

## 目录职责

- `Assets/_Project`：项目自有资源，可以按项目需要修改。
- `Assets/ThirdParty`：外部导入的完整资源包，尽量不要拆散包内结构。
- `Assets/TextMesh Pro`：Unity 生成的 TextMesh Pro 资源，保留默认位置。

`Assets` 根目录只保留以上三个一级目录，避免项目资源和第三方资源散落。

## 常用存放位置

- 人物模型：`Assets/_Project/Art/Characters/Models`
- 人物材质：`Assets/_Project/Art/Characters/Materials`
- 人物贴图：`Assets/_Project/Art/Characters/Textures`
- 人物 Prefab：`Assets/_Project/Art/Characters/Prefabs`
- 动画片段：`Assets/_Project/Animations/Clips`
- Animator Controller：`Assets/_Project/Animations/Controllers`
- Avatar Mask：`Assets/_Project/Animations/AvatarMasks`
- Animator Override Controller：`Assets/_Project/Animations/Overrides`
- 场景：`Assets/_Project/Scenes`
- 代码：`Assets/_Project/Scripts`
- 渲染与项目资源设置：`Assets/_Project/Settings`
- 战斗编辑器能力配置：`Assets/_Project/CombatEditor/ScriptableObjects`
- Master Stylized FX：`Assets/ThirdParty/MasterStylizedFX`
- Red Clue 刀光特效：`Assets/ThirdParty/Red_clue`

环境、道具、UI、音频和特效分别放入 `Art/Environment`、`Art/Props`、`UI`、`Audio` 和 `VFX`。

## 导入和命名规则

1. 第三方包先完整导入，再整包归入 `ThirdParty`；不要把包内模型、材质和动画拆到项目目录。
2. 自制或已经确认要长期维护的资源放进 `_Project`。
3. 在 Unity 的 Project 窗口中移动资源，让资源与 `.meta` 始终一起移动；不要单独删除或重新生成 `.meta`。
4. 文件和目录使用清楚的英文 PascalCase 名称，避免新增空格和含义不明的缩写。
5. Controller 建议按角色命名，例如 `Player.controller`、`EnemyKnight.controller`。
6. 动画片段建议使用 `角色_动作_变体`，例如 `Player_Run_Forward`、`Player_Attack_Heavy_01`。

当前已有的 `9CG_Sword.controller` 已放入 `Assets/_Project/Animations/Controllers`，Sword Animation Pack 已作为完整第三方包归档。
