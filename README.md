# STM32H7 I2C

The provided `ecos_stm32h723_i2c` ELF file executes on a STM32H723 based
platform using I2C to access a AsahaKASEI AK4493S DAC device.

Currently this is a **mirror** (and not a fork) of the
renode-issue-reproduction-template because github limits users to a
single fork.

## branches

| Branch  | Description
|:--------|:-------------------------------------------------------------------
| `main`  | test fails to execute successfully against latest and stable renode worlds
| `fixed` | updated models to allow successful execution of the application
