# DriveSyncPing

O **DriveSyncPing** é uma ferramenta para organizar arquivos locais e enviá-los automaticamente para o **Google Drive**.

Com este aplicativo, o usuário pode escolher **quais pastas deseja enviar**, configurar como os arquivos serão organizados e definir **dias e horários para que o processo de upload seja executado automaticamente**.

## 🚀 Funcionalidades Principais

- 📁 **Seleção de Pastas:** Escolha facilmente as pastas locais a serem monitoradas.
- 🗂️ **Organização Flexível:** Agrupe arquivos por tipo, extensão ou data.
- ☁️ **Upload Automático:** Sincronização direta com o Google Drive, criando as pastas necessárias automaticamente.
- 🔍 **Identificação de Duplicados:** Validação via hash (SHA-256) para evitar reenvio de arquivos já existentes no Drive.
- ✅ **Validação Rigorosa:** Confirmação da integridade do arquivo antes de considerá-lo como "enviado".
- 🗑️ **Exclusão Segura (Opcional):** Remoção dos arquivos locais somente após a validação bem-sucedida do upload.
- 🔄 **Retomada de Processos:** Possibilidade de retomar uploads interrompidos sem duplicar envios.
- ⏰ **Agendamento Avançado:** Definição de horários e dias específicos da semana para execuções automatizadas.
- 🧪 **Simulação (Dry-Run):** Valide quais arquivos seriam manipulados sem aplicar nenhuma alteração real.

## 🏗️ Arquitetura

O projeto utiliza **C# / .NET** com foco em ser multiplataforma. Foi estruturado sob os preceitos de **Clean Architecture** para manter o código escalável e testável, possuindo as seguintes camadas e componentes:

- **`DriveSyncPing.Domain`:** Núcleo da aplicação com as regras e entidades de negócio.
- **`DriveSyncPing.Application`:** Lógica da aplicação, casos de uso e orquestração.
- **`DriveSyncPing.Infrastructure`:** Comunicação externa (SQLite via EF Core, Google Drive API, Acesso a Arquivos).
- **`DriveSyncPing.App`:** Interface gráfica Desktop, construída utilizando **Avalonia UI** e o padrão **MVVM**.
- **`DriveSyncPing.Worker`:** Serviço em segundo plano dedicado para o monitoramento e agendamento contínuo dos uploads.

## 🛠️ Tecnologias Utilizadas

- **.NET (C#)**
- **Avalonia UI** (Interface Desktop Multiplataforma)
- **SQLite & Entity Framework Core** (Armazenamento de logs, status dos envios e configurações)
- **Google Drive API** (Integração na nuvem)
- **xUnit, Moq e FluentAssertions** (Testes automatizados)

## ⚙️ Como executar o projeto

1. **Clone o repositório:**
   ```bash
   git clone https://github.com/seu-usuario/DriveSyncPing.git
   cd DriveSyncPing
   ```

2. **Restaure as dependências e compile a Solução:**
   ```bash
   dotnet build DriveSyncPing.slnx
   ```

3. **Inicie o Aplicativo Desktop:**
   ```bash
   dotnet run --project DriveSyncPing.App
   ```

4. *(Opcional) Executar testes:*
   ```bash
   dotnet test DriveSyncPing.Tests
   ```

## 📜 Licença

Desenvolvido para propósitos de estudos e MVP.
