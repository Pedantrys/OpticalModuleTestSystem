# 仪器命令清单（请粘贴设备手册中对应的 SCPI / 控制命令）

说明：
- 请将每台仪器的 *IDN? 返回（如："Keysight,86100D,xxxx,fw"）与该仪器常用命令段一并粘贴。
- 每台仪器用一个小节并按下面格式填写，便于我自动比对并调整 GpibCommunicator.cs。

格式示例：

## 仪器：Temptronic ATS-545
*IDN? 返回：
> Temptronic,ATS-545,12345,1.0

常用命令（一行一个）：
- 设置目标温度（示例）：SETP <value>
- 选择负温通道（示例）：SETN 2
- 启动吹气/控温（示例）：FLOW 1
- 读取当前设定温度（示例）：TEMP?
- 停止控温（示例）：FLOW 0

---

## 仪器：热流仪 (示例)
*IDN? 返回：
> ACME,THERM-1000,FW,1.2

常用命令：
- 查询温度：:MEAS:TEMP? 或 TEMP?
- 打开吹气：FLOW 1 或 OUTPUT ON
- 关闭吹气：FLOW 0 或 OUTPUT OFF

---

## 仪器：Keysight 86100D
*IDN? 返回：
> KEYSIGHT,86100D,XXXX,FW

常用命令：
- 切换到发射通道（Tx）：
- 切换到接收通道（Rx）：
- 查询眼图参数：:EYE:HEIGHT? / :EYE:WIDTH? / :EYE:JITTER?

---

请把每台仪器按照上述格式粘贴到本文件（或追加新的小节）。完成后回复我一声，我将自动读取并逐条比对并给出修改建议或直接提交对 GpibCommunicator.cs 的更新。