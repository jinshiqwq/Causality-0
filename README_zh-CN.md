# Causality-0

🌍 [English](README.md) | [简体中文](README_zh-CN.md)

<p align="center">
  <img alt="platform" src="https://img.shields.io/badge/platform-SCP%3ASecret%20Laboratory-6f42c1">
  <img alt="api" src="https://img.shields.io/badge/api-LabAPI-2ea44f">
  <img alt="runtime" src="https://img.shields.io/badge/runtime-.NET%20Framework%204.8.1-512bd4">
  <img alt="protocol" src="https://img.shields.io/badge/protocol-.c0%20V13-0a7ea4">
  <img alt="timeline" src="https://img.shields.io/badge/timeline-deterministic-1f6feb">
  <img alt="status" src="https://img.shields.io/badge/status-pre--release-orange">
  <img alt="license" src="https://img.shields.io/badge/license-AGPL--3.0-red">
</p>

<p align="center">
  <strong>面向 SCP:SL 回合的确定性回放引擎</strong>
</p>

<p align="center">
  <em>一局结束后，仍然可以被重新唤回</em>
</p>

---

## 项目概述

Causality-0 是一个基于 LabAPI 的 SCP:SL 服务端回放插件
它会把回合中的服务器侧状态录制成确定性时间线，保存为 `.c0` 二进制回放文件，并在之后通过 Dummy、世界状态重建与种子校验把整局重新带回服务器中

这个项目更关心可复现性，而不是只做一层表面演出
只要能稳定恢复录制结果，就尽量不去依赖脆弱的实时再模拟

---

## 当前能力

### 确定性时间轴回放

回放时间由帧索引与文件内 FPS 元数据共同驱动
这样人物移动、交互、投掷物和可选语音都能绑定在同一条时间线上

### 角色生命周期支持

当前回放链已经支持：

- 录制开始后中途加入的玩家
- 回合结束前离开或断线的玩家
- 回合中的角色切换
- 死亡生命周期事件
- 按起始帧延迟生成 Dummy
- 在录制的离开帧正确退场

### 世界状态持久化

回放文件现在不再只保存演员轨道
当前世界重建已经覆盖：

- 录制开始时地图上的掉落物快照
- 掉落物创建与移除事件
- 掉落物位移持久化
- locker 与 chamber 内物品内容
- load 后柜体与腔室开关状态恢复

### 投掷物持久化

投掷物轨迹现在会真正写入 `.c0`
并且会保存所有者信息，回放时不再依赖原始实时环境

### 门的确定性回放

门交互回放现在优先恢复录制结果本身
不再依赖回放时重新跑一次实时权限判定，因此稳定性更高，也避免了常见的穿门现象

### 可选语音录制

语音包录制已经支持配置开关
即使关闭录音，已有回放文件中的语音数据仍然可以正常加载与播放

### 地图种子校验加载

回放文件内会保存录制时地图种子
如果 load 时发现当前回合种子不一致，插件可以安排下一局切换到录像种子后再加载

---

## `.c0` 协议

当前回放协议版本为 **V13**

当前保存内容包括：

| 字段 | 说明 |
| --- | --- |
| 地图种子 | 用于校验世界是否正确 |
| 回放 FPS | 会写入文件并在加载时恢复 |
| 角色轨道 | 位置、视角旋转、移动状态、持物、数值 |
| 语音包 | 可选原始语音负载与时间戳 |
| 交互帧 | 当前已实现的门交互时间点与结果 |
| 生命周期事件 | 角色切换、死亡、离开/断线 |
| 世界掉落物 | 录制开始时的掉落物快照 |
| 掉落物操作流 | 创建、位移、移除 |
| 柜体状态 | chamber 内容与开关状态 |
| 柜体操作流 | 录制到的柜体交互结果 |
| 投掷物轨道 | 投掷物逐帧数据与所有者 id |

---

## 当前已录制内容

- 玩家位置与视角旋转
- 移动状态与触地状态
- 当前持物与枪械配件码
- 开火与换弹输入
- 消耗品开始与取消使用输入
- HP 与 AHP 类数值
- 可选原始语音包
- 门交互时间点与结果
- 中途加入与离开生命周期变化
- 投掷物轨迹与所有者 id
- 世界掉落物与掉落物位移
- locker / chamber 内容与状态
- 角色切换与死亡生命周期事件

---

## 指令面

`c0` 仍然可以作为 `causality` 的缩写别名使用

```bash
causality start
causality stop
causality save <name>
causality load <name>
causality spawn
causality play

c0 start
c0 stop
c0 save <name>
c0 load <name>
c0 spawn
c0 play
```

### 当前行为说明

- `start` 会从当前状态开始一条新录制
- `save` 会把当前回放写入 `CausalityRecords/<name>.c0`
- `load` 会先读取种子与 FPS 元数据
- 种子一致时，`load` 会先重建世界状态
- 种子不一致时，插件会安排使用录像种子的下一局再加载
- `play` 会启动确定性时间线回放
- `spawn` 仍可用于手动生成 Dummy 的工作流

---

## 配置

插件现在已经支持通过 LabAPI 插件配置目录下的 `config.yml` 进行配置

当前配置项：

```yml
default_record_fps: 60
record_voice: false
```

### 当前配置行为

- `default_record_fps`
  - 设置新录制的默认帧率
  - 只影响之后新开始的录制
  - 不会改变旧回放文件中已经写入的 FPS

- `record_voice`
  - 控制是否在新录制中保存玩家语音包
  - 关闭后，非语音数据仍会正常录制
  - 旧的含语音回放文件仍可正常加载与播放

---

## 关键源码入口

- [Causality0.cs](Causality0.cs)
- [Causality0Config.cs](Causality0Config.cs)
- [Core/Timeline.cs](Core/Timeline.cs)
- [Core/Serializer.cs](Core/Serializer.cs)
- [Core/ActorTrack.cs](Core/ActorTrack.cs)
- [Core/ProjectileTrack.cs](Core/ProjectileTrack.cs)
- [Core/LifecycleEvent.cs](Core/LifecycleEvent.cs)
- [Core/WorldData.cs](Core/WorldData.cs)
- [Command/RemoteAdmin/Causality.cs](Command/RemoteAdmin/Causality.cs)
- [Event/PlayerEvent/Verified.cs](Event/PlayerEvent/Verified.cs)
- [Event/PlayerEvent/Lifecycle.cs](Event/PlayerEvent/Lifecycle.cs)
- [Event/PlayerEvent/VoiceChat.cs](Event/PlayerEvent/VoiceChat.cs)
- [Event/PlayerEvent/Interacting.cs](Event/PlayerEvent/Interacting.cs)
- [Event/PlayerEvent/Lockers.cs](Event/PlayerEvent/Lockers.cs)
- [Event/ServerEvent/Pickups.cs](Event/ServerEvent/Pickups.cs)
- [Event/ServerEvent/MapGenerating.cs](Event/ServerEvent/MapGenerating.cs)

---

## 当前限制

项目目前仍处于预发布阶段
当前已知仍在继续完善的部分包括：

- 尸体 / 布娃娃 / 死亡现场生态持久化
- 某些特殊柜体与展台结构的专用恢复边界情况
- 自动录制整局与 autosave 策略
- 更广范围的世界交互回放覆盖

---

## Roadmap

- [x] 确定性回放时间轴
- [x] 回放文件内嵌 FPS 元数据
- [x] 地图种子校验加载
- [x] 晚加入玩家录制与回放
- [x] 离开 / 断线退场回放
- [x] 可配置语音录制开关
- [x] 门交互录制与确定性回放
- [x] 投掷物持久化与回放
- [x] 世界掉落物快照与位移持久化
- [x] locker / chamber 状态持久化
- [ ] 自动整局录制与 autosave 策略
- [ ] 尸体 / 布娃娃持久化
- [ ] 更多特殊结构世界恢复修复
- [ ] 回放检查与调试工具

---

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=MiaoMiao4567/Causality-0&type=Date)](https://star-history.com/#MiaoMiao4567/Causality-0&Date)

---

## 许可证

本项目使用 [GNU AGPL v3](LICENSE.txt) 许可证
