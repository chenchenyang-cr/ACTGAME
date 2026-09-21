loadMaxFile @"D:\BaiduNetdiskDownload\039-雅\max\雅\Unagi_Attack_01.max" quiet:true
python.ExecuteFile @"D:\19356\UnityLearning\animation_inspect\fix.py"
if doesFileExist @"D:\19356\UnityLearning\animation_inspect\Unagi_Attack_01_fixed.max" do (
loadMaxFile @"D:\BaiduNetdiskDownload\039-雅\max\雅\Unagi_Attack_01.max" quiet:true
python.ExecuteFile @"D:\19356\UnityLearning\animation_inspect\verify.py"
)
quitMax #noPrompt
