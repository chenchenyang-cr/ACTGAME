# Unagi / 雅 动画资源

项目：`D:\19356\UnityLearning`

Unity 资源位置：`Assets/ThirdParty/UnagiAnimationPack`

本包包括 149 个 FBX：Main 套 70 个角色动作，MAX_Variant 套 70 个角色动作、7 个 Ghost 动作以及 2 个基础模型。两套版本按原始来源分别保存；每套下继续按攻击、待机、跑步、行走、闪避、受击、击飞、切换、反弹、死亡分类。

`Textures` 包含 10 张贴图。`manifest.json` 记录 MAX 来源和导出帧范围。

## 使用动画

在 Project 窗口展开某个动作 FBX，可找到与文件同名的 AnimationClip。选中片段可预览；也可以将片段拖入 Animator Controller 使用。

当前以 Generic 模式导入，适用于本包自带骨架。Ghost 使用独立骨架。如需将动作应用到不同的现有 Humanoid 角色，需要另做 Humanoid 映射或重定向。

基础模型在 `FBX/MAX_Variant/Unagi/00_Model` 和 `FBX/MAX_Variant/Ghost/00_Model`，不导入动画。其余 FBX 保留各自的模型、蒙皮、表情和单个动作片段。

`Unagi_Idle`、`Unagi_Run`、`Unagi_Walk` 和 `Unagi_Ghost_Idle` 开启 Loop Time；起步、结束、攻击、受击等动作不自动循环。原始位移保留。动画压缩关闭，保留逐帧数据和表情。

## 检查

### Swordman Main 套导入修复

`FBX/Main/Unagi` 的 70 个动作统一使用 Scale Factor `100`、Motion Node `Root`、Generic Root Motion Bone `Bip001`，与当前 Swordman 待机模型一致。旧配置 Scale Factor `1` 且运动根节点为空，会造成换动画时骨架尺寸和根运动异常。MAX_Variant 与 Ghost 未套用这组设置。

菜单 `Tools > Unagi > Repair Swordman Main Animations` 可修复 Main 套导入，并在同一个待机模型上采样每个动作的 5 个时间点检查骨骼比例。该工具保留资源 GUID、Clip 帧范围、循环、事件及各动作已有的根运动选项，不重配连段或保存场景。检查报告位于 `Library/SwordmanAnimationRepairReport.txt`。

本次检查：70 个导入设置通过，修复 59 个；70 个片段共 350 个采样点的骨骼比例通过。此检查针对缩放异常，不代替所有动作的视觉和玩法验收。通用导入菜单也已保留现有 Clip 配置，避免重新验证资源包时覆盖调整过的事件和根运动参数。

菜单 `Tools > Unagi > Configure and Validate Imported Animations` 可重新配置并检查本包。

检查报告在项目的 `Library/UnagiImportReport.json`，包含每个 FBX 的蒙皮网格、动画片段和表情数量。脚本只配置本包内的模型导入设置。

材质显示需匹配项目使用的 URP Shader。M 贴图的通道含义未作猜测或自动转换。
