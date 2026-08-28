# OnlineJudge 测试样例编写指南

> 适用项目：OnlineJudge  
> 适用判题模式：标准输入输出、Function Mode、Challenge 算法任务  
> 适用语言：C++17、C11、C#  
> 当前 Function Mode 已支持自定义结构类型以及 `CustomType[]` 一维结构数组。

---

## 1. 编写测试样例的目标

测试样例不是“随便准备几组能跑的数据”，而是用尽量少的测试点覆盖题目的正确性边界。

一组质量较好的测试应至少覆盖：

1. **题面样例**：帮助答题者理解输入、输出或函数参数。
2. **最小边界**：例如 `n = 0`、`n = 1`、空数组、空字符串。
3. **普通情况**：代表大多数合法输入。
4. **极值情况**：接近题目允许的最大/最小值。
5. **容易写错的情况**：用于区分正确算法和常见错误算法。
6. **复杂度压力**：用于识别超时算法。
7. **多测试点权重设计**：Challenge 中用于合理计算部分得分。

不要只写“随机数据”。随机数据可以补充覆盖，但不能替代明确的边界测试。

---

# 2. 标准输入输出题

标准输入输出题使用：

- `Input`
- `ExpectedOutput`

判题程序把 `Input` 作为标准输入传给用户程序，再将用户标准输出与 `ExpectedOutput` 比较。

## 2.1 示例：A + B

题目要求：

```text
输入两个整数 a b，输出 a + b。
```

测试点：

### 测试点 1：普通情况

Input：

```text
1 2
```

ExpectedOutput：

```text
3
```

### 测试点 2：负数

Input：

```text
-10 3
```

ExpectedOutput：

```text
-7
```

### 测试点 3：零

Input：

```text
0 0
```

ExpectedOutput：

```text
0
```

## 2.2 多行输入

假设题目为：

```text
第一行 n
第二行 n 个整数
输出所有整数之和
```

Input：

```text
5
1 2 3 4 5
```

ExpectedOutput：

```text
15
```

输入中的换行应该和题目格式保持一致。

## 2.3 输出格式

如果答案需要输出：

```text
YES
```

不要写成：

```text
Yes
```

如果题目要求：

```text
1 2 3
```

不要写成：

```text
1,2,3
```

当前标准判题会规范行尾差异并忽略末尾多余空白，但不要依赖这一行为来编写含糊测试。

---

# 3. Sample 与 Hidden

每个测试点都有可见性。

## Sample

Sample 测试点用于：

- 题面展示。
- 帮助用户理解格式。
- 提供最基本的调试数据。

Sample 应尽量：

- 简单。
- 人工可以验证。
- 不泄露隐藏边界。

## Hidden

Hidden 测试点用于真正判题。

Hidden 应重点覆盖：

- 边界。
- 极值。
- 特殊结构。
- 错误算法反例。
- 性能压力。

推荐：

```text
Sample：1～3 个
Hidden：根据题目复杂度准备 5～20 个或更多
```

不要把所有关键反例都放在 Sample 中。

---

# 4. Function Mode 基本格式

Function Mode 不使用普通标准输入输出。

每个测试点主要填写：

- `ArgumentsJson`
- `ExpectedJson`

假设函数签名：

```text
twoSum(nums: int[], target: int) -> int[]
```

ArgumentsJson：

```json
{
  "nums": [2, 7, 11, 15],
  "target": 9
}
```

ExpectedJson：

```json
[0, 1]
```

## 4.1 ArgumentsJson 的规则

ArgumentsJson 必须是一个 JSON 对象。

字段名必须和函数参数名**完全一致**。

正确：

```json
{
  "nums": [2, 7],
  "target": 9
}
```

错误——缺少参数：

```json
{
  "nums": [2, 7]
}
```

错误——包含额外字段：

```json
{
  "nums": [2, 7],
  "target": 9,
  "debug": true
}
```

错误——字段名写错：

```json
{
  "numbers": [2, 7],
  "target": 9
}
```

---

# 5. Function Mode 基础类型

## int

FunctionSpec：

```text
value: int
```

ArgumentsJson：

```json
{
  "value": 10
}
```

不能使用：

```json
{
  "value": 10.5
}
```

## long

```json
{
  "value": 2147483648
}
```

适合超过 32 位整数范围的数据。

## double

```json
{
  "x": 1.5
}
```

浮点返回值比较允许微小误差，但测试数据仍建议使用清晰、可验证的值。

## bool

```json
{
  "enabled": true
}
```

只能使用 JSON：

```json
true
false
```

不要使用：

```json
1
0
"true"
```

## string

```json
{
  "name": "UESTC"
}
```

---

# 6. 数组

## int[]

```json
{
  "nums": [1, 2, 3, 4]
}
```

空数组：

```json
{
  "nums": []
}
```

## double[]

```json
{
  "values": [1.5, 2.5, 3.5]
}
```

## string[]

```json
{
  "names": ["A", "B", "C"]
}
```

## int[][]

```json
{
  "matrix": [
    [1, 2],
    [3, 4]
  ]
}
```

建议矩阵题至少覆盖：

- `1 × 1`
- 单行
- 单列
- 普通矩阵
- 最大规模

---

# 7. ListNode<int>

链表在测试 JSON 中使用整数数组表示。

函数：

```text
reverseList(head: ListNode<int>) -> ListNode<int>
```

ArgumentsJson：

```json
{
  "head": [1, 2, 3, 4]
}
```

表示：

```text
1 -> 2 -> 3 -> 4
```

ExpectedJson：

```json
[4, 3, 2, 1]
```

空链表：

ArgumentsJson：

```json
{
  "head": []
}
```

ExpectedJson：

```json
[]
```

不要使用：

```json
{
  "head": null
}
```

当前链表 JSON 协议使用数组，不使用 `null` 表示空链表。

C11 Function Mode 当前不支持 `ListNode<int>`。

---

# 8. TreeNode<int>

二叉树使用层序数组表示。

函数：

```text
invertTree(root: TreeNode<int>) -> TreeNode<int>
```

ArgumentsJson：

```json
{
  "root": [4, 2, 7, 1, 3, 6, 9]
}
```

ExpectedJson：

```json
[4, 7, 2, 9, 6, 3, 1]
```

可以使用 `null` 表示中间缺失节点：

```json
{
  "root": [1, 2, 3, null, 4]
}
```

空树：

```json
{
  "root": []
}
```

C11 Function Mode 当前不支持 `TreeNode<int>`。

---

# 9. 自定义结构类型

Function Mode 支持通过 FunctionSpec 定义结构类型。

例如：

```text
Point3
├─ x: double
├─ y: double
└─ z: double

Triangle
├─ a: Point3
├─ b: Point3
└─ c: Point3
```

对应 FunctionSpec：

```json
{
  "types": [
    {
      "name": "Point3",
      "fields": [
        { "name": "x", "type": "double" },
        { "name": "y", "type": "double" },
        { "name": "z", "type": "double" }
      ]
    },
    {
      "name": "Triangle",
      "fields": [
        { "name": "a", "type": "Point3" },
        { "name": "b", "type": "Point3" },
        { "name": "c", "type": "Point3" }
      ]
    }
  ],
  "functionName": "solve",
  "returnType": "double",
  "parameters": [
    { "name": "triangle", "type": "Triangle" }
  ]
}
```

测试点：

ArgumentsJson：

```json
{
  "triangle": {
    "a": { "x": 0, "y": 0, "z": 0 },
    "b": { "x": 1, "y": 0, "z": 0 },
    "c": { "x": 0, "y": 1, "z": 0 }
  }
}
```

ExpectedJson：

```json
0.5
```

## 9.1 字段必须完全匹配

如果 `Point3` 定义了：

```text
x
y
z
```

正确：

```json
{
  "x": 1,
  "y": 2,
  "z": 3
}
```

错误——少字段：

```json
{
  "x": 1,
  "y": 2
}
```

错误——多字段：

```json
{
  "x": 1,
  "y": 2,
  "z": 3,
  "w": 4
}
```

---

# 10. 自定义结构数组：Triangle[] / Segment3[]

这是几何题最常用的形式。

定义：

```text
Point3
Triangle
Segment3
```

函数：

```text
geometryScore(
    triangles: Triangle[],
    segments: Segment3[]
) -> double
```

ArgumentsJson：

```json
{
  "triangles": [
    {
      "a": { "x": 1, "y": 2, "z": 3 },
      "b": { "x": 4, "y": 5, "z": 6 },
      "c": { "x": 7, "y": 8, "z": 9 }
    }
  ],
  "segments": [
    {
      "a": { "x": 10, "y": 11, "z": 12 },
      "b": { "x": 13, "y": 14, "z": 15 }
    }
  ]
}
```

空数组必须专门测试：

```json
{
  "triangles": [],
  "segments": []
}
```

建议结构数组题至少准备：

1. 两个数组都为空。
2. 一个为空、一个非空。
3. 两个都只有一个元素。
4. 多个结构元素。
5. 坐标包含零。
6. 坐标包含负数。
7. 浮点坐标。
8. 较大规模数据。

---

# 11. 当前自定义结构边界

当前支持自定义结构的字段：

```text
int
long
double
bool
string
其他自定义结构
```

顶层函数参数/返回值支持：

```text
CustomType
CustomType[]
```

当前暂不支持结构内部数组字段，例如：

```text
Polygon
└─ points: Point3[]
```

因此不要编写：

```json
{
  "types": [
    {
      "name": "Polygon",
      "fields": [
        { "name": "points", "type": "Point3[]" }
      ]
    }
  ]
}
```

如果确实需要 Polygon，现阶段优先把 `Point3[]` 直接作为函数参数。

另外，C11 自定义结构字段当前不支持 `string`。

---

# 12. ExpectedJson 的原则

ExpectedJson 只包含**函数返回值本身**。

函数：

```text
sum(nums: int[]) -> int
```

正确：

```json
6
```

错误：

```json
{
  "result": 6
}
```

函数：

```text
getPoint() -> Point3
```

正确：

```json
{
  "x": 1,
  "y": 2,
  "z": 3
}
```

函数：

```text
getTriangles() -> Triangle[]
```

正确：

```json
[
  {
    "a": { "x": 0, "y": 0, "z": 0 },
    "b": { "x": 1, "y": 0, "z": 0 },
    "c": { "x": 0, "y": 1, "z": 0 }
  }
]
```

---

# 13. Challenge 测试点 Score

Challenge 算法任务支持测试点权重。

测试点的 `Score` 是**内部权重**，不是直接加到用户总分。

假设 Challenge Task 满分：

```text
300
```

测试点：

| 测试点 | Score |
|---|---:|
| Case 1 | 20 |
| Case 2 | 30 |
| Case 3 | 50 |

总测试点权重：

```text
100
```

用户通过 Case 1 + Case 2：

```text
通过权重 = 50
```

最终 Challenge Task 得分：

```text
300 × 50 / 100 = 150
```

因此推荐一个任务的测试点权重总和使用：

```text
100
```

这样最容易理解和维护。

并不是强制必须为 100；系统按：

```text
通过权重 / 总权重
```

计算比例。

---

# 14. Challenge 权重设计建议

不要让一个测试点占绝大多数分数，除非它确实代表任务核心。

例如不推荐：

| Case | Score |
|---|---:|
| 基础 | 1 |
| 普通 | 1 |
| 极限 | 98 |

这样用户只因为一个极限点失败就几乎没有分数。

更合理：

| 类型 | 权重 |
|---|---:|
| 基础正确性 | 20 |
| 普通情况 | 30 |
| 边界 | 20 |
| 特殊反例 | 15 |
| 性能压力 | 15 |

总计：

```text
100
```

---

# 15. 部分得分与完成状态

Challenge 中：

```text
通过部分 Case
```

可以获得部分得分，但：

```text
Completed = false
```

只有整个任务最终 Accepted 时：

```text
Completed = true
```

例如任务满分 300：

```text
通过 50 / 100 权重
→ Score = 150
→ Completed = false
```

之后用户提交更差答案：

```text
90 分
```

历史最佳仍保持：

```text
150
```

之后 AC：

```text
300
Completed = true
```

所以设计 Challenge 测试点时，要保证权重分配确实能反映“完成程度”。

---

# 16. 如何设计能抓出错误算法的测试

以“判断数组是否严格递增”为例。

错误实现可能只检查：

```text
a[i] <= a[i + 1]
```

测试必须包含重复元素：

```json
{
  "nums": [1, 2, 2, 3]
}
```

期望：

```json
false
```

再例如求最大值，错误实现可能把初始最大值写成 `0`。

必须加入：

```json
{
  "nums": [-10, -5, -20]
}
```

ExpectedJson：

```json
-5
```

原则：

> 每想到一种常见错误写法，就尽量准备一个能让它失败的测试点。

---

# 17. 性能测试

如果正确算法要求：

```text
O(n log n)
```

而暴力算法：

```text
O(n²)
```

隐藏测试必须有足够大的数据，否则两种算法都会通过。

但不要盲目把数据做到最大。

推荐：

1. 小规模验证正确性。
2. 中规模验证一般情况。
3. 接近上界验证复杂度。
4. 确保正确算法在 TimeLimit 下有合理余量。

例如正确程序实际约：

```text
200 ms
```

TimeLimit 不建议直接设为：

```text
210 ms
```

应保留 Docker、机器波动和语言差异余量。

---

# 18. 内存测试

题目的 `MemoryLimitMb` 是**用户程序运行阶段**的限制。

编译阶段已经与题目运行内存解耦，因此不要为了避免 C++ 编译器 OOM 而把题目的运行内存限制人为调大。

设置 MemoryLimit 时应考虑的是：

```text
正确算法实际运行内存
```

而不是：

```text
g++ 编译需要多少内存
```

---

# 19. 推荐测试点模板

普通算法题可以按下面的结构准备：

| 序号 | 类型 | 可见性 | 权重 | 目的 |
|---|---|---|---:|---|
| 1 | 最简单样例 | Sample | 10 | 说明格式 |
| 2 | 普通情况 | Sample | 10 | 说明主要逻辑 |
| 3 | 最小边界 | Hidden | 10 | 检查边界 |
| 4 | 特殊值 | Hidden | 15 | 检查错误假设 |
| 5 | 常见错误反例 | Hidden | 20 | 区分错误算法 |
| 6 | 普通大数据 | Hidden | 15 | 稳定性 |
| 7 | 极限数据 | Hidden | 20 | 复杂度 |

如果是 Challenge，可将总权重调整为 100。

---

# 20. 创建测试点前检查清单

提交测试数据前逐项检查：

- [ ] 题面与测试数据使用同一种输入协议。
- [ ] Sample 能人工计算结果。
- [ ] 有最小边界。
- [ ] 有空数组/空结构的测试（如果允许）。
- [ ] 有负数/零（如果允许）。
- [ ] 有最大或接近最大规模。
- [ ] 有至少一个常见错误算法反例。
- [ ] Function Mode 参数字段名与函数签名完全一致。
- [ ] JSON 数字类型与 FunctionSpec 匹配。
- [ ] 自定义结构字段不缺失、不多余。
- [ ] 自定义结构数组包含空数组测试。
- [ ] ExpectedJson 只描述返回值。
- [ ] Challenge 测试点权重分配合理。
- [ ] Hidden 中包含真正用于判题的关键反例。
- [ ] 正确参考代码已经跑过所有测试点。

---

# 21. 推荐的最终验证流程

新增或修改题目后，至少自己提交：

```text
1. 一份正确答案
2. 一份明显错误答案
3. 一份容易通过 Sample、但会在 Hidden 失败的答案
```

理想结果：

```text
正确答案
→ Accepted

明显错误答案
→ WrongAnswer

边界错误答案
→ Sample 可能通过
→ Hidden WrongAnswer
```

对于 Challenge：

```text
部分正确答案
→ 获得合理部分分
→ Completed = false

完全正确答案
→ 满分
→ Completed = true
```

对于自定义结构题，应至少分别用：

```text
C++17
C11
C#
```

各验证一次，避免只验证某一种 CodeBuilder。

---

# 22. 最重要的原则

测试样例设计的目标不是：

> 证明一份正确代码能通过。

而是：

> 在正确代码能够稳定通过的同时，尽可能让错误代码暴露出来。

一个只有题面 Sample 的 OJ，实际上几乎没有真正的判题能力。

高质量测试集应该同时具备：

```text
正确性覆盖
+ 边界覆盖
+ 错误算法区分能力
+ 性能压力
+ 合理评分权重
```
