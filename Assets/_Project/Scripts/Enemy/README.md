# 敌人系统：Root Motion 群战框架

当前版本采用“顶层状态机 + Combat 战术子状态机”的结构。目标不是让没有攻击令牌的敌人停下来排队，而是让所有参战敌人持续执行可读的空间行为。

## 1. 总体结构

顶层状态机继续负责敌人的生命周期：

```text
Inactive → Alert → Chase → Combat
                         ├→ Stagger
                         └→ Dead
```

进入 `Combat` 后，由一个战术子状态机描述敌人当前唯一的战斗行为：

```text
ApproachSlot
→ Orbit ↔ Pressure
       ↘ Yield → ApproachSlot

Orbit / ApproachSlot
→ 取得导演令牌
→ MoveToAttackRange
→ ExecuteAttack
→ AttackRecovery
→ Retreat
→ Orbit
```

不再额外维护 `AttackAuthority`、`SpatialState` 或战斗黑板。`CurrentCombatTactic` 是战斗状态的唯一来源；本轮选中的攻击由战术状态机直接保存，真实令牌持有关系由导演维护。攻击结束后立即归还令牌，敌人必须退回对峙环带且等待冷却结束，才能重新申请攻击权。

## 2. 文件结构

```text
Enemy/
├─ AI/
│  └─ EnemyBrain.cs
├─ Combat/
│  └─ EnemyCombatAdapter.cs
├─ Coordination/
│  └─ EncounterCombatDirector.cs
├─ Core/
│  ├─ EnemyCombatTypes.cs
│  ├─ EnemyConfig.cs
│  └─ EnemyController.cs
├─ Movement/
│  └─ EnemyMotor.cs
└─ StateMachine/
   ├─ EnemyStateMachine.cs
   ├─ EnemyCombatState.cs
   └─ Combat/
      ├─ EnemyCombatTacticalStateMachine.cs
      ├─ EnemyApproachSlotTactic.cs
      ├─ EnemyOrbitTactic.cs
      ├─ EnemyPressureTactic.cs
      ├─ EnemyYieldTactic.cs
      ├─ EnemyMoveToAttackRangeTactic.cs
      ├─ EnemyExecuteAttackTactic.cs
      ├─ EnemyRecoverTactic.cs
      └─ EnemyRetreatTactic.cs
```

当前没有引入行为树。行为数量仍然有限，脚本状态机更容易断点调试；将来接入成熟的可视化行为树工具后，可以把复杂决策下沉，但不需要替换战斗导演和移动层。

## 3. 空间状态职责

### ApproachSlot

敌人进入 Combat 后首先判断自己是否位于分配的扇形环带内。如果不在，只寻找到该区域最近边界的短路径；进入区域后立即转为 `Orbit`。这里不存在需要持续追逐的槽位中心点。

### Orbit

没有令牌时的默认状态。敌人始终面向玩家，在自己的扇形环带内自由移动：

- 每个移动周期在区域内选择一个临时随机目标点，并持续移动到该点。
- 抵达目标点或移动超时后，按 `Orbit Wait Chance` 随机决定是否停顿。
- 需要停顿时，先完整播放 Stop 并稳定进入 Idle，再计算额外停顿时间，最后选择下一个目标点。
- 不停顿时保持 Locomotion，直接选择下一个临时目标点，方向变化仍由 `EnemyMotor` 平滑。
- 从 `ApproachSlot / Pressure / Retreat` 进入 `Orbit` 时也使用同一等待门，不会携带上一段移动直接切换方向。
- 行走 0.8～2 秒。
- 停顿 0.3～0.8 秒。
- 每个敌人使用不同随机相位。
- 移动方向以横向为主，并允许一定比例的前后试探。
- 接近扇区角度边界或内外半径边界时自动改变方向。
- 到达扇区边缘时先播放短暂停步，再从反方向重新起步，避免左右动画硬切。
- 停顿结束后有概率进入 `Pressure` 或反转方向。

### Pressure

没有令牌的敌人短暂向内环靠近一步，制造即将攻击的压力，然后回到 `Orbit`。当前只使用八方向 Root Motion 移动，不播放会造成伤害的攻击动画。

### Yield

导演把持有令牌者到玩家之间视为攻击通道。其他敌人进入通道时会沿侧向让开，通道恢复后重新 `ApproachSlot`。

### Retreat

攻击完成或攻击接近失败后进入。敌人释放令牌、清除本轮攻击并退回自己扇形区域的最近边界。重新进入区域且冷却结束后恢复为 `Orbit`，之后可以再次申请令牌。

## 4. 战斗导演职责

`EncounterCombatDirector` 只负责群体约束，不直接操作 Animator：

- 注册和移除参战敌人。
- 维护攻击令牌和公平等待队列。
- 维护持久的“敌人 → 槽位编号”占用表，每个槽位同一时间只属于一个有效敌人。
- 敌人第一次申请区域时，从所有未占用槽位中选择离当前位置最近的一个，而不是按注册顺序随机重排。
- 玩家平移时整组区域只做平移，已有敌人的槽位编号和区域内临时目标参数保持不变。
- 首次分配会锁定整组区域的世界朝向，玩家之后转身不会带着槽位旋转或交换敌人。
- 敌人退出、禁用或死亡时释放槽位，新加入的敌人再选择最近的空槽位。
- 只有有效敌人数导致槽位总数发生变化时，才会按当前位置重新执行一次最近空槽分配。
- 输出扇区角度边界、活动半径范围、径向和切向，不输出固定槽位目标点。
- 将空间目标投影到 NavMesh 并检查完整路径。
- 预留空间目标，减少多个敌人选中同一点。
- 检测攻击通道并输出让位方向。

导演当前仍使用公平队列发放令牌。距离、镜头可见性、敌人类型权重和令牌抢占属于后续扩展，不在这一版提前实现。

Main 场景默认参数：

| 参数 | 默认值 | 作用 |
|---|---:|---|
| Maximum Concurrent Attackers | 0 | 同时允许的真实进攻者数量；0 表示不发放新令牌，正式战斗通常设为 1 |
| Minimum Slot Count | 4 | 玩家周围最少扇区数 |
| Inner Ring Radius | 2.7 | 进攻和施压参考内环 |
| Outer Ring Radius | 6.5 | 对峙扇形区域的最外半径 |
| Confrontation Region Depth | 2.8 | 对峙区域从外环向内延伸的深度；默认活动半径为 3.7～6.5 |
| Sector Boundary Padding | 5 | 相邻扇区边界之间预留的角度 |
| Slot Arrival Tolerance | 0.3 | 进入扇形区域边界时的距离容差 |
| NavMesh Sample Distance | 1.5 | 理想站位落在 NavMesh 外时，搜索附近可行走点的半径 |
| Minimum Enemy Spacing | 0.8 | 敌人和预留位置的最小间距 |
| Angular Offset | 30 | 本次遭遇首次分配时，整组扇区相对目标朝向的旋转角度 |
| Close Gap Threshold | 0.12 | 超出扇形区域多少距离后强制重新入区 |
| Orbit Minimum Target Distance | 0.8 | 新随机目标点与敌人当前位置需要保持的最小距离 |
| Orbit Radial Freedom | 0.35 | 自由方向中允许的前后分量；0 为纯横移 |
| Orbit Walk Duration Min / Max | 1 / 2 | 朝当前随机目标点移动的最长时间范围（秒） |
| Orbit Wait Chance | 0.65 | 每次换目标点时进入完整 Stop→Idle 等待流程的概率 |
| Orbit Idle Duration Min / Max | 0.3 / 0.5 | Stop 动画完整结束并进入 Idle 后的额外观察停顿范围（秒） |
| Orbit Target Retry Pause Min / Max | 0.18 / 0.32 | 随机点不可用时，完整停稳后重新选点前的等待范围（秒） |
| Pressure Chance | 0.3 | 停顿后进入施压的概率 |
| Orbit Reverse Chance | 0.2 | 停顿后将下一个目标点选在当前角度另一侧的概率 |
| Pressure Step Distance | 0.8 | 施压向内移动距离 |
| Pressure Duration | 0.9 | 一次施压的最长持续时间（秒） |
| Attack Corridor Width | 0.9 | 触发让位的通道半宽 |
| Yield Distance | 0.9 | 一次让位的侧向距离 |
| Yield Duration | 0.7 | 一次让位的最长持续时间（秒） |

### 临时关闭所有攻击

将 `Maximum Concurrent Attackers` 设为 `0` 后，导演不会发放新的攻击令牌。敌人仍会执行入位、环绕、施压和让位，也仍可进入受击状态，因此适合单独测试受击反馈。

建议在进入 Play Mode 前设置为 `0`。如果在已有敌人进入攻击动作后才改为 `0`，已经提交的当前攻击会正常收尾，之后不再产生新攻击；这样可以避免强行中断攻击动画和伤害窗口。恢复正式战斗时改回 `1` 即可。

`Tools → Enemy → Configure Main Scene Combat Director` 会保留已有导演的令牌数；首次创建导演时使用脚本默认值 `0`。

选中 `Combat Director` 后，Gizmos 会显示攻击内环、对峙环带、扇区分界线、预留点和当前攻击通道。橙色线是区域边界，不再显示槽位中心球。

## 5. Root Motion 移动链

```text
空间状态选择区域内临时目标点或最近区域边界
→ NavMeshAgent 生成短路径和当前 steeringTarget
→ EnemyMotor 转换为面向玩家时的局部方向
→ 八方向 Blend Tree 选择 RM 动画
→ RootMotionReceiver 收集动画增量
→ RootMotionParentApplier 推动敌人根节点
```

关键设置：

- `Animator.applyRootMotion = true`
- `NavMeshAgent.updatePosition = false`
- `NavMeshAgent.updateRotation = false`
- 对峙与攻击接近时由代码保持朝向玩家
- NavMeshAgent 不直接写 Transform

Animator 参数：

| 参数 | 作用 |
|---|---|
| MoveX / MoveY | 连续的局部横移、前进和后退方向 |
| MoveSpeed | 归一化期望速度 |
| StartX / StartY | 起步瞬间冻结的八方向输入 |
| StopX / StopY | 停步瞬间冻结的八方向输入 |
| IsMoving | 当前是否存在有效移动请求 |

Animator 状态检测使用短名称 Hash，同时兼容完整路径，避免 `LocomotionStart` 被反复从第 0 帧重播。`MoveTo/Stop` 会维护明确的移动请求；移动方向由“角色真实 Transform 到当前路径拐点”计算，不直接使用 `NavMeshAgent.isStopped` 或 Agent 内部可能提前归零的 `desiredVelocity` 控制融合树。

Locomotion 使用完整的 `IsMoving` 过渡环：`Idle → Start → Loop → Stop → Idle`。`Start` 中途停止会融合到 `Stop`，`Stop` 中途重新移动会融合回 `Start`。移动状态之间不再由代码直接 CrossFade，避免 Animator 过渡和脚本同时抢占状态；攻击、受击等非移动状态返回移动时仍使用安全 CrossFade。

SwordEnemy Controller 使用 SwordAnimationPack 的 24 个 Humanoid Root Motion 动画：八方向 Start、八方向 Loop、八方向 Stop。模型必须保留 `9CG_SwordAvatar`，不能在场景实例上覆盖为 `None`。

`EnemyMotor.Configure` 会主动解析 `NavMeshAgent` 和子节点 Animator，不依赖同一 GameObject 上多个组件的 Awake 调用顺序，避免启动时把实际存在的 Blend Tree 参数误判为缺失。

移动方向不会直接采用 NavMesh 每帧给出的瞬时拐点方向。`EnemyMotor` 会限制方向变化角速度，并过滤小角度抖动，使 `MoveX/MoveY` 沿八方向融合树相邻区域连续变化。`Orbit` 会保留当前临时目标点直到抵达或超时，再执行“Stop → 停顿 → 选择新点 → Start”，不会每帧把目标点推到敌人前方。

敌人对峙朝向由 `EnemyMotor.FaceTarget` 独占。`RootMotionParentApplier` 会保留动画位移，但过滤普通移动动画的根旋转，避免横移动画在 LateUpdate 把敌人转向移动方向，从而把本应输入 `MoveX` 的切线运动错误地变成 `MoveY`。`LocomotionStart / LocomotionLoop / LocomotionStop` Tag 用于识别并平滑 Start→Loop 的 Root Motion 速度差。

## 6. 配置与生成

重新生成敌人 Prefab：

```text
Tools → Enemy → Rebuild Sword Enemy Prefab
```

重新配置 Main 场景导演与敌人 Animator：

```text
Tools → Enemy → Configure Main Scene Combat Director
```

敌人个体数值继续集中在单个 `EnemyConfig` 中，群战数值配置在场景导演上。Approach、Orbit、Pressure、Yield 和 Retreat 不创建额外 SO。

## 7. 验收清单

1. 三个敌人进入 Combat 后分别占据距离自己最近且未被占用的独立扇形区域。
2. 没有令牌的敌人会在各自区域内自由横移、前后试探、停顿和转向，不围绕中心点往返。
3. 玩家移动时，槽位会跟随，敌人会主动重新入位。
4. 只有持令牌者能进入真实攻击链。
5. 进攻者穿过等待敌人时，等待者会侧向让位。
6. 攻击结束者先退回外环，再重新申请令牌。
7. 横移和后退时，Animator 的 MoveX/MoveY 与实际方向一致。
8. 实际位移来自 Root Motion，NavMeshAgent 不直接拖动角色。

## 8. 受击与命中反馈

攻击伤害配置在动作编辑器的 `CreateHitBox` 事件中，不放入敌人个体配置：

- `Single`：碰撞盒存续期间，每个伤害接收者只结算一次。
- `Repeated`：第一次接触立即结算，随后按照 `Repeat Interval Frames` 的 60 FPS 动作帧间隔重复结算。
- 多个 Collider 通过 `ICombatDamageReceiver` 归并为同一目标，离开后再进入也不会重置当前伤害窗口的命中次数。
- 玩家和敌人分别提供 Team，默认阻止同阵营与自身命中。
- 敌人的生命、削韧恢复与死亡由 `EnemyDamageReceiver` 管理；受击动作可配置为首段、每段或仅削韧击破时触发。
- `CameraShake` 使用 `OnConfirmedHit` 时只响应实际结算成功的命中，其监听区间必须覆盖目标 HitBox 区间。

`Combo_Attack_01_02` 已配置一段单次伤害示例；在动作编辑器中复制该 HitBox 并改为 `Repeated`，即可测试固定帧多段伤害。

## 9. 暂未实现

- Feint、Taunt、Threaten 动作层。
- 多个 Combat Idle 变化。
- 摄像机可见性和屏幕内外攻击评分。
- 精英敌人类型权重和令牌抢占。
- 动态跨槽位换位。
- 玩家生命值与玩家受击结算。

这些内容应在当前空间状态稳定后逐项增加，不需要继续扩大基础框架。
