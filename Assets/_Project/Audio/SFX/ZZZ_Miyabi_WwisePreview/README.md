# 星见雅相关音效：已知 Wwise 参数试听版

本目录现有 9 个根据游戏包声音层制作的处理版，另有 `ZZZ_ParryAid_EventPreview_A/B.wav` 两个组合试听版。生成时没有修改原始素材；新增 03/06 受击音的原始解码 WAV 保存在相邻的 `ZZZ_Miyabi_Impact_Raw/`。这些是**按已确认的部分参数制作的试听版，不是《绝区零》游戏内最终混音**。普通攻击 WAV 已单独整理到相邻的 `ZZZ_Miyabi_Attack_Separated/NormalAttack/`；End WAV 在其 `End/` 目录，两组没有合成。

处理规则：把对应 SoundBank 对象层级的固定音量值相加并应用到音源；攻击音应用近似的一阶低通；弹反 Switch A/B 按 −1200 cents 以半速播放；弹反三个音源前加 100 ms 静音。所有输出统一为 44.1 kHz、双声道、16 位 PCM。原 Switch A/B 的 6000 Hz 源音频升采样不会恢复原本不存在的高频。

项目内敌人受击播放版 `Miyabi_Impact_Normal_*.wav` 在上述参数试听结果上统一额外提高了 **4 dB**，方便在当前战斗混音中听清。这个增益是本项目的播放调整，不属于原版 Wwise 参数；03/06 的未加增益原始解码版另存于 `ZZZ_Miyabi_Impact_Raw/`。`SwordEnemyConfig.asset` 的 `hitSoundVolume` 仍为 1。

| 文件 | 已应用的参数 |
| --- | --- |
| `ZZZ_Miyabi_Attack_Separated/NormalAttack/Miyabi_Attack_Normal_01_A/B.wav` | −5 dB，LPF 20；原事件的随机音高取中点 0 cents |
| `ZZZ_Miyabi_Attack_Separated/NormalAttack/Miyabi_Attack_Normal_02_A/B.wav` | −7 dB，LPF 20 |
| `ZZZ_Miyabi_Attack_Separated/NormalAttack/Miyabi_Attack_Normal_03.wav`、`Miyabi_Attack_Normal_06.wav` | −5 dB，LPF 20 |
| `ZZZ_Miyabi_Attack_Separated/NormalAttack/Miyabi_Attack_Normal_05.wav` | −4 dB，LPF 20；原事件的随机音高取中点 0 cents |
| `Miyabi_Impact_Normal_01.wav` | −9 dB，LPF 23 |
| `Miyabi_Impact_Normal_02.wav` | −8 dB，LPF 23 |
| `Miyabi_Impact_Normal_03_1.wav` | −8 dB，LPF 23 |
| `Miyabi_Impact_Normal_03_2.wav` | −6 dB，LPF 23 |
| `Miyabi_Impact_Normal_06_1.wav` | −4 dB，LPF 23 |
| `Miyabi_Impact_Normal_06_2.wav` | −8 dB，LPF 23 |
| `Miyabi_ParryAid_Heavy/Light_Foley.wav` | −9 dB |
| `ZZZ_ParryAid_Core.wav` | −1 dB，延迟 100 ms |
| 弹反组合试听 A 所用的切换层（未单独保留 WAV） | −0.5 dB，降八度，延迟 100 ms；随机音量取范围中点 −1.5 dB |
| 弹反组合试听 B 所用的切换层（未单独保留 WAV） | −2.5 dB，降八度，延迟 100 ms；随机音量取范围中点 −1.5 dB |

`LPF 20/23` 是 Wwise 的相对值，不是 Hz。试听版参考 Wwise 的近似对照表，以一阶低通分别使用约 11.3 kHz / 10.0 kHz；滤波算法未证明与游戏一致。

`ZZZ_ParryAid_EventPreview_A/B.wav` 分别将主体音与一种切换音按上述延迟叠加。叠加峰值会超过 16 位 PCM 范围，因此额外施加统一主增益以避免削波：A 为 0.5536，B 为 0.6160。这是试听便利处理，不是游戏参数。两个版本是备选，不应同时播放。

未应用直达输出的 −96 dB：原事件还经辅助总线发送，若只保留直达输出会近乎静音。辅助总线插件、运行时 RTPC、状态、距离衰减、实际随机值及完整混音尚未还原。使用本目录时请把它作为参数试听素材，不要标注为“游戏原声最终版”。
