# 按键弹反攻击

配置场景：`Assets/_Project/Scenes/Main_Nodachi.unity`（Unagi 玩家）。键盘 E / 手柄 RB 只读取按下事件，一次按下只执行一次动作。

按键当下，检查正在攻击的敌人当前动画进度是否位于其 `ParryOpportunityWindow`，并检查玩家与敌人根节点的水平距离。距离不大于玩家 `PlayerCombatAdapter → Parry Attack Range`（当前 2.25 米）时，选择成功弹反分支；有多个敌人符合条件时选择最近的。无需等待碰撞或受到攻击。

两条分支都播放 `Unagi_Attack_Branch_01_Attack_01.anim`，但使用不同的动作编辑器技能资源与事件轨道：

- `Unagi_Attack_Branch_01_Attack_01_Normal.asset`：没有符合条件的敌人时播放，保留原普通分支的事件轨道配置。
- `Unagi_Attack_Branch_01_Attack_01_Parry.asset`：条件满足时播放，保留原成功弹反分支的独立事件轨道；普通攻击的命中盒不会在该分支运行。

两条分支的 `Unagi_Attack_Branch_01_Attack_01.anim` 播放结束后，都会转入 `Unagi_Attack_Branch_01_Attack_01_End.anim`。End 使用独立的 `Unagi_Attack_Branch_01_Attack_01_End.asset` 轨道；通常在 End 完成后恢复操作，设置的 `InterruptWindow` 可允许提前打断。

普通、成功、End 三个技能分别读取当前轨道上的 `InterruptWindow`：`AllowedCommands` 决定 Dodge、LightAttack、Parry 等命令是否可取消，`AllowMovement` 决定方向输入能否取消。窗口外输入不会立即打断；仍处于输入缓存有效期内的命令可在之后的有效窗口执行。当前普通/成功轨道的末段允许 Dodge；End 全段允许 Dodge、LightAttack 和移动。

成功分支在按键时取消目标敌人当前攻击的命中盒，使其进入硬直（当前 0.8 秒），并触发玩家的弹反音效与顿帧。目标敌人的残余攻击命中在成功动作期间不会对玩家造成伤害。其他敌人的攻击仍正常结算。硬直和距离分别在玩家的 `PlayerCombatAdapter` 上调整。

敌人技能 `SwordEnemy_Attack_01/02.asset` 已各有一个独立的 `ParryOpportunityWindow`：初始为 `[0.07, 0.25)` 和 `[0.08, 0.35)`。可在敌人动作编辑器中调节；没有此窗口的敌人动作不能触发该弹反。

场景中玩家的 `PlayerDamageReceiver` 与 `PlayerParryFeedback` 已启用。旧的受击时判定弹反逻辑仍保留给未配置 `ordinaryParryAttack` / `successfulParryAttack` 的其他场景。`Main_Nodachi` 使用新的按键即时判定流程。

本机批处理 Unity 导入被已打开的同一项目实例阻止；当前完成了 `dotnet` 编译，Unity Inspector 导入和 Play 模式手感仍需在已打开的编辑器里验证。建议检查：窗口前/中/后按键、距离内/外、多个敌人、普通与成功轨道是否互斥、两条分支是否都完整播放 End 后才恢复操作、成功后敌人是否停招、玩家是否免受该次攻击伤害。
