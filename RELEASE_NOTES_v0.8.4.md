# Comm Terminal v0.8.4

- FWP3 新增 READY 握手：发送头部后等待 `06 FF FF`，并忽略 Bootloader 调试文本后再发送第一个固件包。
