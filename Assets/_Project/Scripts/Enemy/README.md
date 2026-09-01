# 敌人系统：单敌人落地版

当前阶段的目标是先让一个敌人完整跑起来，而不是提前建设群战系统。

本版只保留：发现目标、警戒、追击、进入战斗、选择招式、移动到攻击距离、播放攻击、攻击后恢复、受击硬直和死亡。战斗导演、进攻令牌、环形站位和敌人角色分工暂不接入，等两个以上敌人同时战斗时再扩展。

## 1. 当前结构

```text
Enemy/
├─ AI/
│  └─ EnemyBrain.cs                    选择当前可用的攻击
├─ Combat/
│  └─ EnemyCombatAdapter.cs            把敌人决策接到现有 Ability/Animator
├─ Core/
│  ├─ EnemyCombatTypes.cs              状态与战术枚举
│  ├─ EnemyConfig.cs                   单一敌人配置资源
│  └─ EnemyController.cs               组件入口与依赖协调
├─ Movement/
│  └─ EnemyMotor.cs                    NavMesh 移动与朝向
└─ StateMachine/
   ├─ EnemyStateMachine.cs             顶层状态机
   ├─ EnemyInactiveState.cs            待机与目标检测
   ├─ EnemyAlertState.cs               发现目标后的警戒停顿
   ├─ EnemyChaseState.cs               追击目标
   ├─ EnemyCombatState.cs              战斗阶段入口
   ├─ EnemyStaggerState.cs             受击硬直
   ├─ EnemyDeadState.cs                死亡
   └─ Combat/
      ├─ EnemySelectAttackTactic.cs     选招
      ├─ EnemyMoveToAttackRangeTactic.cs移动到招式距离
      ├─ EnemyExecuteAttackTactic.cs    执行攻击
      └─ EnemyRecoverTactic.cs          攻击后恢复
```

## 2. 状态链路

顶层状态机负责敌人的大阶段：

```text
Inactive → Alert → Chase → Combat
    ↑                  │
    └── 目标丢失 ──────┘

任意存活状态 → Stagger → Chase 或 Combat
任意存活状态 → Dead
```

- `Inactive`：原地待机。目标进入检测距离后进入警戒。
- `Alert`：播放发现目标的动画并面向目标，给玩家一个反应时间。
- `Chase`：使用 NavMesh 追击；进入战斗距离后切换到 Combat。
- `Combat`：运行下面的轻量战术子状态机。
- `Stagger`：停止移动和当前攻击，播放受击动画；结束后按距离回到追击或战斗。
- `Dead`：停止移动和攻击、关闭导航并播放死亡动画。

Combat 内部只处理一次攻击循环：

```text
SelectAttack → MoveToAttackRange → ExecuteAttack → Recover
      ↑                                             │
      └─────────────────────────────────────────────┘
```

- `SelectAttack`：从未冷却且距离可达的攻击中，按优先级和距离选择一招。
- `MoveToAttackRange`：移动到该招式的理想距离，同时转向玩家。
- `ExecuteAttack`：通过 `EnemyCombatAdapter` 播放 `AbilityScriptableObject` 对应的动画。
- `Recover`：保留攻击后摇，再进入下一次选招。

这不是完整行为树。当前行为数量少、流程固定，脚本状态机更容易调试。将来出现巡逻、搜索、掩体、协作、逃跑等大量可组合行为，并且已有可视化行为树工具时，再把复杂决策下沉到行为树。

## 3. 现在只创建一个 EnemyConfig

在 Project 窗口选择：

```text
Create → Enemy → Enemy Config
```

一个敌人原型只需要这一个敌人专属 SO。每一招仍然引用项目已有的 `AbilityScriptableObject`，不再为角色、动画、站位和战斗导演分别创建 SO。

建议先使用以下数值：

| 配置 | 建议值 | 作用 |
|---|---:|---|
| Detection Distance | 8 | 开始发现玩家的距离 |
| Lose Target Distance | 12 | 超出后脱战，需大于检测距离 |
| Alert Duration | 0.5 | 发现玩家后的警戒时间 |
| Chase Speed | 3.5 | 追击速度 |
| Combat Speed | 2.2 | 攻击范围内调整位置的速度 |
| Combat Enter Distance | 4 | 从追击进入战斗的距离 |
| Chase Resume Distance | 5 | 玩家拉开后恢复追击的距离，形成滞回避免反复切状态 |
| Decision Interval | 0.25 | 重新选招的最短间隔 |
| Attack Approach Allowance | 2 | 允许为攻击额外接近目标的距离 |
| Post Attack Recovery | 1 | 通用攻击后摇 |

第一版只配置一招：

| 攻击配置 | 建议值 |
|---|---:|
| Ability | 选择一份已经可播放的 Ability |
| Minimum Range | 0.8 |
| Maximum Range | 2.3 |
| Cooldown | 1.2 |
| Priority | 1 |
| Facing Tolerance | 20 |
| Entry Tolerance | 0.35 |

第一版仍建议先用位移较少的招式验证攻击距离。移动动画和带位移攻击的实际推进距离由动画 Root Motion 决定，NavMeshAgent 的 Speed 主要用于生成寻路期望速度和方向，不会直接推动 Transform。

## 4. 敌人 Prefab 配置步骤

项目内已经提供一个使用 `SwordAnimationPack` 的可运行样例：

```text
Assets/_Project/Enemies/SwordEnemy/Prefabs/SwordEnemy.prefab
```

它已经配置好模型、Animator Controller、NavMeshAgent、CapsuleCollider、CombatController、EnemyConfig 和一段基础斩击。将 Prefab 拖入已烘焙 NavMesh 的场景即可开始测试；场景玩家根节点需要使用 `Player` 标签。

样例采用清晰的逻辑与表现分层：

```text
SwordEnemy                         游戏逻辑根节点
├─ NavMeshAgent / CapsuleCollider
├─ CombatController
├─ EnemyController / EnemyBrain
├─ EnemyMotor / EnemyCombatAdapter
├─ EnemyStateMachine
└─ Visual_9CG_Sword                第三方模型与骨骼
   └─ Animator
```

`Visual_9CG_Sword` 直接实例化 `SwordAnimationPack/Model/9CG_Sword.FBX`，并复用该模型已经配置好的 Humanoid Avatar。这样模型的完整骨骼层级和 Avatar 都会保留下来，不需要再手工创建骨骼或把 Avatar 留空。

样例 Animator Controller 仍使用敌人专用的精简状态结构，因为敌人代码依赖 `Idle`、`Alert`、`Locomotion`、`Hit`、`Death` 和攻击片段同名状态；项目现有玩家 Controller 的分层和状态路径不适合直接交给敌人逻辑驱动。不过，Controller 中的 Motion 全部引用资源包 `Animation/Humanoid` 目录下已有的 `.anim` 动画，不会复制动画，也不会再误选 `Animation/FBX` 目录中的同名 Generic Clip。

敌人现在使用 Root Motion 移动：子节点 Animator 产生位移，`RootMotionReceiver` 收集动画增量，根节点 `RootMotionParentApplier` 把增量应用到 `SwordEnemy`。`NavMeshAgent` 的 `Update Position` 和 `Update Rotation` 保持关闭，只负责路径、期望移动方向和避障；每帧会同步到动画实际移动后的位置，避免 Agent 与 Animator 同时写 Transform 造成抖动。`CombatController/Allow Motion Translation` 保持开启，使带位移的攻击动画也能正常推进角色。

若再次看到“Generic clip 已被 Humanoid Animator 绑定”的警告，优先检查 Controller 或 Ability 的动画引用路径：它们必须来自 `SwordAnimationPack/Animation/Humanoid`，而不是 `SwordAnimationPack/Animation/FBX`。

当前样例 Ability 只负责驱动攻击动画，事件列表暂时为空，因此不会创建伤害盒。这与当前“先落地敌人行为、暂不实现受击闭环”的阶段保持一致。

在敌人根节点添加：

1. `NavMeshAgent`
2. `CombatController`（现有 3C 战斗系统组件）
3. `EnemyController`
4. `EnemyBrain`
5. `EnemyMotor`
6. `EnemyCombatAdapter`
7. `EnemyStateMachine`

模型子节点需要 `Animator`。`EnemyController`、`EnemyMotor` 和 `EnemyCombatAdapter` 会自动从子节点寻找 Animator，因此通常不必手动拖三遍。

然后完成以下引用：

1. 将创建的 `EnemyConfig` 拖到 `EnemyController/Config`。
2. Prefab 会在运行时自动寻找带有 `Player` 标签的对象；也可以在场景实例上手动覆盖 `EnemyController/Target`。
3. 确保 EnemyConfig 中使用的 Ability 已加入该敌人 `CombatController` 的能力数据列表。
4. 确保 Animator Controller 中存在 Idle、Alert、Locomotion、Hit、Death，以及攻击动画片段同名的状态。
5. 若动画状态名不同，直接在 EnemyConfig 的 Animation 区域修改字符串。
6. 烘焙场景 NavMesh，并确认敌人出生点位于 NavMesh 上。

`MoveSpeed` 参数是 0～1 的归一化移动速度，用于驱动移动 Blend Tree。如果当前 Animator 没有该参数，可将 EnemyConfig 中的参数名留空。

## 5. 最小验收顺序

不要一开始同时测试全部功能，按下面顺序排查：

1. 玩家进入 8 米后，敌人从 Idle 播放 Alert。
2. Alert 结束后，敌人能沿 NavMesh 追向玩家。
3. 进入 4 米后，敌人切入 Combat，并靠近到招式范围。
4. 面向误差满足配置后，播放 Ability 对应攻击动画。
5. 动画结束或 Ability 的退出窗口允许退出后，进入 Recover，再次选招。
6. 在运行时调用 `EnemyController.NotifyStagger()`，确认攻击被打断并播放 Hit。
7. 调用 `EnemyController.NotifyDied()`，确认进入死亡状态且不再移动。

若不攻击，优先检查：Target 是否赋值、敌人是否在 NavMesh 上、Ability 是否有 AnimationClip、Animator 状态名是否与 Clip 名一致。

## 6. 当前边界与后续扩展

当前版本已经为受击闭环保留两个入口：

```csharp
enemyController.NotifyStagger();
enemyController.NotifyDied();
```

伤害结算、受击特效和摄像机震动尚未在这里实现。后续接事件系统时，建议由伤害接收组件改变生命与硬直状态，再发布“命中结果事件”；敌人特效和摄像机震动分别订阅事件。这样表现可以独立配置和测试，不会反向耦合敌人 AI。

当场景中真正出现多个敌人抢攻、围堵和互相遮挡的问题时，再新增群战层：

```text
EncounterCombatDirector
├─ 进攻令牌：限制同时出手数量
├─ 对峙站位：分配目标周围的槽位
└─ 敌人角色：近战、远程、压迫、支援等偏好
```

群战层应该给单敌人状态机提供“是否允许攻击”和“应该站在哪里”的约束，不替代单敌人自身的追击与攻击执行。这样当前 Prefab 无需推倒重做。

如果更换了 SwordAnimationPack 资源或希望重新生成样例，可使用 Unity 菜单：

```text
Tools → Enemy → Rebuild Sword Enemy Prefab
```
