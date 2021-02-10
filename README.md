# Chika
Chika是一个适用于公主连结简中版 (https://game.bilibili.com/pcr/) 的竞技场排名自动刷新程序。  
名字来自于游戏角色 [三角千歌 (Misumi Chika)](https://zh.moegirl.org.cn/index.php?title=%E4%B8%89%E8%A7%92%E5%8D%83%E6%AD%8C)。

## 使用
在新版本中，您需要在启动路径创建一个```chika_account_v2.json```文件。  
内部为一个JArray，按以下方式保存您的B站登录账号密码。
```json
[
  {
    "username": "这里是账号",
    "password": "这里是密码"
  }
]
```
您需要至少2个账号以完整运行Chika的功能。  
并于打码平台```http://www.ydaaa.com/```中创建一个用户账号。（用于识别B站登录的Geetest验证码）    
创建文件```ydaaa.txt```，第一行为您的用户名，第二行为您的appkey。  
模拟SDK登录可能对对抗风控有效，但目前尚未测试。  

**以下配置文件构造方法为老版本方法，新版本见上方说明**
### 构造```chika_account.json```
使用Fiddler对游戏进行抓包，获取路径为```/tool/sdk_login```的请求（如何抓包这里不详细说明了）。  
![sdk_login](https://s3.ax1x.com/2021/01/29/yisxoT.png)  
打开该请求，在请求中的```Inspectors```面板下的```HexView```内，从黑色字体(Body部分)开始选择到结尾，右键```Copy -> Copy as Base64```。
![body](https://s3.ax1x.com/2021/01/29/yiy1OI.png)  
打开根目录下的```chika_helper.html```。  
将之前复制的Base64粘贴到上侧，点击添加。  
![copy](https://s3.ax1x.com/2021/01/29/yiyy7V.png)  
您需要添加至少两个账号才能完整使用Chika的功能。  
新建一个名为```chika_account.json```的文件，放置在编译后的程序根目录。  
您也可以参考[pcrjjc2](https://github.com/qq1176321897/pcrjjc2)使用其他方法来构造您的```chika_account.json```。  

## 通信方式
### 正向通信
Chika最初被设计用于QQ机器人，因此部分接口以QQ号为识别单位，但Chika本身没有与QQ机器人相关的代码。  
以下为简略说明接口，完整的请求与响应体请参考代码。  
  
更新某个群内QQ号对应的角色  
```POST /account/update```  
获取QQ号对应角色的竞技场信息  
```GET /account/profile/{qq}```  
获取QQ号对应角色最近5次竞技场记录  
```GET /account/logs/{qq}```  
删除某个群内某个QQ号对应的角色  
```DELETE /account/remove/{qq}/{groupId}```  
  
### 反向通信
[RefreshService.cs](https://github.com/Kengxxiao/Chika/blob/master/Chika/GameCore/RefreshService.cs#L18)内定义了反向通信使用的服务端。  
反向通信用于把自动刷新的结果推送给指定的服务端（如QQ机器人）。  
  
推送刷新结果（~~Chika最初只用于Himari机器人，因此该接口命名为```himari_chika_api```~~）    
```POST /himari_chika_api/chika_update_2```  
您可以在RefreshService中修改该接口的路径。  
