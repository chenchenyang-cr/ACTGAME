# Swordman 攻击与收招

场景：`Assets/_Project/Scenes/Main_Nodachi.unity`，角色：`Player_Swordman`。

## 当前配置

- 三连击：`Swordman_Attack_01 → Swordman_Attack_02 → Swordman_Attack_03`。
- 动画使用 Controller 中已有的 `Unagi_Attack_01/02/03` 及各自 `_End`。
- 连段窗口：攻击进度 60%～95%，命令 `LightAttack`，成功后消费一次缓存输入。
- 没有接段时：攻击进度 95% 进入本段 End，混合 0.08 秒；End 播完回待机。
- 攻击后 25% 可移动取消；End 后 50% 可移动取消或按攻击从第一段重启。
- 输入缓存沿用场景的 0.35 秒。窗口内输入立即接段，窗口前输入须在缓存过期前等到窗口开放。
- 本次只配置动画与连段窗口，没有新增伤害 HitBox、刀光、音效、闪避或弹反资源。
- 已启用角色的 CombatController，并将六个攻击/收招 FBX 的导入比例及运动根节点与 Main 套待机动画对齐。
- `PlayerCommandInputAdapter` 和 `PlayerCombatAdapter` 必须启用。前者负责将鼠标左键的 LightAttack 事件送入输入缓存；禁用时移动仍可用，但点击不会攻击。

这些进度是初始调试值，需结合实际动画姿势调整。

## 修改连段

1. 打开战斗编辑器中 `Swordman Attacks` 分组，选择某段攻击。
2. 调整 `ComboWindow` 的时间区间；`CommandId` 保持 `LightAttack`。
3. 把 `NextAbility` 指向下一段攻击，开启 `ConsumeBufferedInput`。最后一段不配置续段窗口。
4. 在角色 `PlayerCombatAdapter → Attack Recoveries` 中设置该段的 `Attack`、`End`、`Start Time` 和 `Blend Duration`。连段窗口结束点应与收招点一致。
5. 在 End Ability 中调整 `InterruptWindow`。`AllowMovement` 控制移动取消，`LightAttack` 允许重新起手。
6. 新增段数时，把攻击和 End Ability 加入 `CombatController → CombatDatas`，在 Animator 中添加与各 Clip 同名的状态，并关闭动画循环。

不要给攻击或 End 添加提前退出的 ExitWindow；正常结束由 AttackState 根据实际动画进度处理。已有旧角色未配置 Attack Recoveries 时保留直接退出行为。

`Tools > Unagi > Configure Swordman Attacks 1-3` 可重新绑定基础配置；它保留已有 Ability 事件，但会将首段、三项收招映射和收招数值恢复到上述初始值。执行前打开 Main_Nodachi；此工具保存场景和资源。

## 验收

- 单击：Attack 1 → End 1 → Idle。
- 窗口内再按一次：Attack 1 → Attack 2 → End 2 → Idle。
- 连续三次有效输入：Attack 1 → Attack 2 → Attack 3 → End 3 → Idle。
- End 后半段按攻击：从 Attack 1 重启；按移动：进入移动。
- 检查提前输入、过期输入、低帧率、动画变速下的窗口，以及收招时的根位移和姿势衔接。

自动检查通过 Input System 虚拟鼠标发送左键按下/松开，覆盖输入绑定、PlayerInputReader、PlayerCommandInputAdapter、输入缓存、实际 Animator 和 ComboWindow，检查每条序列进入对应 End 并返回 IdleState。报告：`Library/SwordmanAttackPlayCheckReport.txt`。视觉手感、取消操作和低帧率检查仍需实际操作验收。
