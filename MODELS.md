# 翻译模型选择

核对日期：2026-10-05。以下为官方标准在线 API 价格；不是 ChatGPT/网页会员价格。实际可用模型、账单与网络延迟取决于账户、地域和服务商。未使用真实 API Key 做质量或速度横向实测。

| 模型 | 每百万输入 token | 每百万输出 token | 用于本工具的建议 |
| --- | ---: | ---: | --- |
| Qwen-MT-Flash（北京） | ¥0.7 | ¥1.95 | 默认首选：专用翻译、增量流式、成本低。 |
| Qwen-MT-Plus（北京） | ¥1.8 | ¥5.4 | 对论文难句做质量对照；效果仍需用自己的材料比较。 |
| DeepSeek Flash | $0.15–0.30 | $0.60–1.20 | 通用模型备选，关闭默认思考；非缓存输入，峰谷时段价格不同。 |
| OpenAI GPT-6 Luna | $0.10 | $0.50 | Standard 短上下文轻量模型备选，关闭思考；需有可用账户及网络。 |

Qwen 价格来源：[阿里云计费表](https://help.aliyun.com/zh/model-studio/model-pricing)。北京地域与新加坡等地域价格不同，不能跨地域套用上表。

DeepSeek 价格与峰谷时段来源：[官方价格页](https://api-docs.deepseek.com/quick_start/pricing/)。其 `deepseek-flash` 当前对应 DeepSeek-V4.1-Flash；程序不使用已过时的型号假设。[关闭思考的参数](https://api-docs.deepseek.com/guides/thinking_mode/)。

OpenAI 价格来源：[官方计费表](https://developers.openai.com/api/docs/pricing)，模型定位与非思考支持：[GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna)。未使用 Batch/Flex/缓存输入优惠来低估日常交互费用。

例如，每次含 1,000 输入 token + 1,000 输出 token，Qwen-MT-Flash 的模型费约 ¥0.00265，做 1,000 次约 ¥2.65。只是 token 数量假设，token 不等于汉字数；实际费用按使用量计。

## 推荐方法

先用 Qwen-MT-Flash。挑选十几段实际论文（否定句、术语、长句、数字、引用和公式），对比 Plus 或其他模型。检查遗漏、否定翻转和术语一致性，同时观察工具显示的首段和总耗时。短句通常更受网络往返和排队影响，官方模型定位不能直接证明你本机的延迟。

不默认使用长时间思考、对话历史或解释性输出。先忠实翻译当前选区，保留原文，按需换模型复核。

## 接口实现依据

- [Qwen-MT 工作方式](https://help.aliyun.com/zh/model-studio/machine-translation)：单条 user 消息，使用 `translation_options` 设置源语种与目标语种。
- [Qwen-MT API](https://help.aliyun.com/zh/model-studio/qwen-mt-api)：Flash/Lite 使用增量流式；Plus/Turbo 使用累计序列。当前旧地域域名仍可使用，亦支持业务空间专属域名。
- [DeepSeek Chat Completions](https://api-docs.deepseek.com/api/create-chat-completion/)：使用 `thinking: {type: "disabled"}` 关闭思考。
- [GPT-6 Luna](https://developers.openai.com/api/docs/models/gpt-6-luna)：Chat Completions 与 `reasoning_effort: "none"`。
