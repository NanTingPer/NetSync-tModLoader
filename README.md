# NetSync-tModLoader
 一个帮助模组作者进行联机同步的代码段

本项目本意是供给我正在开发的模组用的（`GensokyoWPNACC`），但是感觉能帮助一些人，所以开源。

命名空间什么的，就自己改一下喽

---

# 功能 :

- 在ModProjectile中，使用特性标记可以让被特性标记的字段 / 属性在设置`Projectile.netUpdate = true`时，一起被同步
- 在ModPlayer中，可以注册`用户自定义字段`，当检测到`ModPlayer`中的`用户自定义字段`为`true`时，会对注册字段时，指定的特性标记字段/属性进行同步



---

# 未来:

- 其他需要进行同步的情形
