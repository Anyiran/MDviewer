# MDviewer 示例文档

一个用于测试 **MDviewer** 渲染效果的 Markdown 文件。它覆盖了常见的 GitHub 风格语法。

## 目录与标题

### 三级标题示例

#### 四级标题示例

##### 五级标题示例

###### 六级标题示例

侧边栏会自动收集以上所有标题，并在滚动时高亮当前章节。

## 文本样式

这是普通段落。支持 **加粗**、*斜体*、***加粗斜体***、~~删除线~~、`行内代码` 和 [链接](https://github.com)。

> 引用：好的工具应当安静地完成工作。
>
> 支持多段引用，以及 **嵌套样式**。

---

## 列表

### 无序列表

- 第一项
- 第二项
  - 嵌套项 A
  - 嵌套项 B
- 第三项

### 有序列表

1. 打开文件
2. 阅读内容
3. 关闭应用

### 任务列表

- [x] 侧边栏目录
- [x] 日间 / 夜间模式
- [x] 字体缩放
- [x] GitHub 风格样式
- [ ] 更多功能

## 代码

行内代码：`const answer = 42;`

```javascript
// 计算斐波那契数列
function fib(n) {
  if (n <= 1) return n;
  return fib(n - 1) + fib(n - 2);
}

const results = [1, 2, 3, 4, 5].map((n) => fib(n));
console.log(results.join(", "));
```

```csharp
public sealed class Greeter
{
    public string Name { get; set; }

    public string Greet()
    {
        // 简单的方法示例
        return "Hello, " + Name + "!";
    }
}
```

```python
def quicksort(items):
    if len(items) <= 1:
        return items
    pivot = items[len(items) // 2]
    left = [x for x in items if x < pivot]
    middle = [x for x in items if x == pivot]
    right = [x for x in items if x > pivot]
    return quicksort(left) + middle + quicksort(right)
```

```json
{
  "name": "MDviewer",
  "version": "1.0.0",
  "features": ["sidebar", "dark mode", "zoom"],
  "offline": true
}
```

```bash
# 构建便携版
powershell -ExecutionPolicy Bypass -File build.ps1
```

## 表格

| 功能 | 快捷键 | 说明 |
| :--- | :---: | ---: |
| 打开文件 | `Ctrl` + `O` | 选择 Markdown 文件 |
| 侧边栏 | `Ctrl` + `B` | 显示或隐藏目录 |
| 查找 | `Ctrl` + `F` | 在文中搜索 |
| 夜间模式 | `Ctrl` + `T` | 切换主题 |
| 字体缩放 | `Ctrl` + `+` / `-` | 调整正文大小 |
| 重置缩放 | `Ctrl` + `0` | 恢复 100% |

## 图片

图片会相对于 Markdown 文件所在的目录解析：

![示例图片](images/example.png)

## 引用与嵌套

> ### 引用中的标题
>
> > 第二层引用
>
> - 引用中的列表
> - 第二项

## 脚注与细节

<details>
<summary>点击展开更多信息</summary>

这里是折叠区域的内容，支持 **Markdown** 语法。

</details>

## 结语

感谢使用 MDviewer。按 `F1` 可以查看关于界面。
