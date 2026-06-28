# Terraform 鐩綍缁撴瀯

杩欎釜鐩綍瀛樻斁 `CloudDocumentPipeline` 鐨?Azure 鍩虹璁炬柦浠ｇ爜銆?

## 缁撴瀯璇存槑

- `modules/`
  - 鍙鐢ㄧ殑 Azure 璧勬簮妯″潡
- `environments/testbed/`
  - testbed 鐜鍏ュ彛
- `environments/prod/`
  - prod 鐜鍏ュ彛

姣忎釜鐜鐩綍鏈韩閮芥槸涓€涓嫭绔嬬殑 Terraform root module锛屾墍浠ワ細

- provider 澹版槑鏀惧湪 `environments/*`
- 鐜鍙橀噺鍜屽懡鍚嶈鍒欎篃鏀惧湪 `environments/*`
- `main.tf` 鍐嶅幓缁勫悎 `modules/*`

## 褰撳墠妯″潡

褰撳墠宸茬粡鏈夌殑妯″潡锛?

- `resource-group`
- `log-analytics`
- `container-app-environment`
- `sql-database`
- `storage-account`
- `service-bus`
- `key-vault`
- `container-app`
- `container-app-job`

## 褰撳墠瑕嗙洊鑼冨洿

### 鍩虹璁炬柦搴曞骇

- Resource Group
- Log Analytics Workspace
- Container Apps Environment
- Azure SQL Server + Database
- Storage Account + Blob Container
- Service Bus Namespace + Topic + Subscriptions
- Key Vault

### 杩愯灞?

- `api`
- `web`
- `worker`
- `notification`
- `migrator` job

### 杩愯鏃跺叧閿厤缃?

- Managed Identity
- Key Vault secret references
- SQL / Blob / Service Bus secret 娉ㄥ叆
- GHCR 绉佹湁闀滃儚鎷夊彇璁よ瘉
- API probes
- worker / notification 鍓湰鍙傛暟
- ingress 缁嗛厤缃?
- revision mode
- migrator job timeout / retry / parallelism

## 璋冪敤鏂瑰紡

姣忎釜鐜鐨勬墽琛岄摼鍙互杩戜技鐞嗚В鎴愶細

```text
terraform.tfvars
-> variables.tf
-> locals.tf
-> main.tf
-> modules/*
-> Azure resources
-> outputs.tf
```

涔熷氨鏄細

- `variables.tf`
  - 瀹氫箟杈撳叆
- `terraform.tfvars`
  - 鎻愪緵鐜瀹為檯鍙傛暟
- `locals.tf`
  - 鎷艰鍛藉悕鍜屼腑闂村€?
- `main.tf`
  - 璋冪敤妯″潡
- `outputs.tf`
  - 鏆撮湶鍏抽敭缁撴灉

## Secret 鍜岄暅鍍忓弬鏁扮害瀹?

- `terraform.tfvars`
  - 鍙繚鐣欓潪鏁忔劅鐜鍙傛暟
- `secrets.auto.tfvars`
  - 鏀炬湰鍦版晱鎰熷€煎拰闀滃儚鍦板潃
  - 宸插姞鍏?`.gitignore`
- `secrets.auto.tfvars.example`
  - 鍙綔涓虹ず渚嬫ā鏉匡紝涓嶆彁浜ょ湡瀹炲€?

甯歌鏈湴鏁忔劅鍙傛暟鍖呮嫭锛?

- `sql_administrator_login_password`
- `sql_connection_string`
- `blob_connection_string`
- `service_bus_connection_string`
- `ghcr_registry_username`
- `ghcr_registry_password`
- `api_image`
- `web_image`
- `worker_image`
- `notification_image`
- `migrator_image`

濡傛灉鍚庣画鎺ュ叆 CI/CD锛屾洿鎺ㄨ崘浣跨敤锛?

- `TF_VAR_*` 鐜鍙橀噺
- GitHub environment secrets

## 褰撳墠鐘舵€?

杩欏 Terraform 涓荤嚎宸茬粡瀹屾垚鍒帮細

- 鍩虹璁炬柦缁撴瀯瀹屾暣
- 杩愯灞傜粨鏋勫畬鏁?
- 杩愯鏃跺叧閿厤缃熀鏈榻愮幇缃?

褰撳墠鏇村儚鈥滃彲钀藉湴鍓嶇殑鏀跺熬闃舵鈥濓紝杩樺樊鐨勪富瑕佹槸锛?

- 鐪熷疄鍊兼敞鍏?
- 绗竴娆＄湡瀹?`terraform plan/apply`
- 鍙€夌殑 remote state backend
- 鍙€夌殑 infra workflow
