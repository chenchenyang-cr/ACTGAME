# 弹反配置

已配置场景：`Assets/_Project/Scenes/Main_Nodachi.unity`。

## 操作

- 键盘 E / 手柄 RB：只读取按下，触发一次 Start → End；成功则 Start → Success → End。长按与短按相同，松开不改变动作。
- 起手、收刀或成功反馈期间，再次按下立即重新起手并开始新一轮弹反判定。一次起手最多成功一次；没有 Hold/Loop 阶段，持续按住不会重复触发。
- 成功播放左右 Parry 动画，并触发火花、金属音和动画顿帧；敌人停止本次攻击并进入硬直。
- 窗口外、背后或不可弹反攻击走普通受击。默认玩家生命 100，归零停止控制；复活流程不在本次功能内。
- 攻击的原有 Dodge 取消窗口也允许 Parry；闪避和受击中不能立即弹反。
- Parry 独立缓存默认 0.12 秒，优先于普通队列；缓存记录的是按下事件，不依赖按键当前是否仍被按住。

## 调整手感

Start 播放到 75% 时开始融合到 End，混合时长为 0.18 秒。可通过 `PlayerAnimationProfile → Parry Start End Transition Time` 调整开始融合的进度；成功弹反仍播放 Success 后再收刀。

1. 在战斗编辑器选择玩家的 Parry 分组，打开 `Parry_Start`。
2. 拖动 `Parry Window` 的起止位置。默认归一化区间 `[0.1, 0.5)`；起手动画长 0.5 秒，正常速度下对应 0.05～0.25 秒（60 FPS 时间轴第 3～15 帧）。实际判定在命中时读取 Animator 进度；松开不影响本次窗口，再次按下从新的起手进度重新计算。
3. 窗口事件上的 `Facing Angle` 默认 120°，`Attacker Stagger Duration` 默认 0.8 秒。
4. 在 `Parry_End` 的 `Recovery Cancel Window` 配置收势取消时机，默认后 40% 可取消到移动、Dodge 或 LightAttack。
5. 攻击 HitBox 事件上的 `Can Be Parried` 决定该攻击能否弹反，默认开启。
6. 玩家 `PlayerInputBuffer` 上的 `Parry Buffer Duration` 控制按键缓存。
7. `PlayerAnimationProfile` 的 Parry 区域控制动画状态名和混合时长；进入收刀的 `Parry End Blend Duration` 为 0.18 秒，其余弹反混合默认 0.06 秒。
8. 玩家 `PlayerParryFeedback` 控制火花、音效、音量、特效比例与顿帧曲线。当前音效是可替换的合成金属音；火花使用现有 StarHit 预制体。

所需动画复制在 `Assets/_Project/Animations/Clips/Player_*.anim`。弹反流程不再使用 Block_Loop，起手与收势均不循环；第三方源动画保持原样。

## 检查

- `Tools > Player > Validate Parry`：检查窗口边界、方向、按键绑定、缓存、起手与收势技能、Animator 状态及循环标志。
- 本次完整新增代码编译通过；窗口边界和资源引用检查见验证工具；弹反输入明确配置为 Press Only。
- Play Mode 验收：E/RB 短按和长按；窗口前、内、后受到攻击；背后与不可弹反攻击；攻击后摇取消；同一攻击多碰撞体/多段命中；按住期间失焦；动画变速；低帧率下的窗口边界。
- 当前环境尚未完成 Play Mode 实机操作验收。

玩家接收器在命中成功后返回已接受的 `Parried` 结果，让 HitBox 正常记录命中；确认事件中关闭本次攻击的碰撞盒并通知敌人硬直，避免重复结算与普通命中反馈叠加。
