# 🚗 GTA 2 Clone (Unity URP)

[![Unity 6 / LTS 2022.3+](https://img.shields.io/badge/Unity-6%20%2F%20LTS%202022.3%2B-blue.svg?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-orange.svg)](https://unity.com/srp/Universal-Render-Pipeline)
[![Input System](https://img.shields.io/badge/Input-New%20Input%20System-green.svg)](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **GTA 2 Clone** é uma recriação moderna da experiência clássica arcade top-down do lendário **Grand Theft Auto 2**, desenvolvida em **Unity (C#)** com estética estilizada **Low-Poly 3D** e renderizada via **Universal Render Pipeline (URP)**.

O projeto foca no loop arcade central de **roubo de veículos sob demanda (*Carjacking*)**, corridas contra o relógio (*Crazy Taxi style*) para entrega em **pontos de desmanche (*Drop-off Zones*)**, navegação em uma malha viária do bairro da **Lapa (Rio de Janeiro)**, sistema de **tráfego urbano inteligente com semáforos** e **perseguições policiais escalonadas de 0 a 6 estrelas**.

---

## 🎮 Gameplay & Mecânicas Principais

### 1. Loop Central Arcade (Contratos de Roubo & Desmanche)
- **Banco de Tempo Global:** Cada partida inicia com um banco de tempo que decresce continuamente. Entregas bem-sucedidas concedem tempo extra e bônus em dinheiro proporcionais à velocidade de entrega.
- **20 Veículos Únicos:** Do compacto ao caminhão pesado, viaturas policiais e tratores, cada veículo possui física de aceleração, velocidade, durabilidade e manobrabilidade balanceadas via `ScriptableObjects` (`VehicleData`).
- **Seta 3D Orbital Inteligente:** Aponta proceduralmente para o veículo-alvo na cor **verde** (`StealVehicle`) e muda para a cor **amarela** (`DeliverVehicle`) apontando para o desmanche ativo assim que o jogador entra no carro.
- **4 Pontos de Desmanche Estratégicos:** Espalhados pela Lapa (*Arcos da Lapa*, *Armazém do Senado*, *Largo do Passeio* e *Mem de Sá Oeste*).

### 2. Física Veicular Top-Down Estilo GTA 2
- **Direção e Tração Híbrida:** Amortecimento viscoso e atrito linear que eliminam o efeito "pista de gelo", permitindo drifts controlados, retomadas ágeis e manobras precisas no teclado ou gamepad.
- **Animação Procedural de Rodas:** Rodas dianteiras esterçam e todas as 4 rodas giram realisticamente de acordo com o deslocamento físico.
- **Danos e Explosões:** Feedback visual de danos no capô (fumaça a 55% de vida, fogo a 28%) culminando na explosão catastrófica do chassi quando a integridade zera.

### 3. Sistema de Tráfego Autônomo & Semáforos
- **Grafo Viário Integrado (`TrafficRoadGraph`):** Mais de 16 veículos de IA circulando ativamente com curvas de Bézier quadráticas nos cruzamentos e algoritmo *Pure-Pursuit* nas retas.
- **Cruzamentos com Semáforos Reais:** Ciclos de sinal Verde, Amarelo e Vermelho com postes iluminados por *Point Lights* procedurais e controle de reserva para prevenir colisões em conversões à esquerda.
- **Sistema Anti-Deadlock:** Veículos de IA calculam rotas alternativas, dão ré e realizam desobstrução automática caso fiquem travados.

### 4. Polícia — Wanted Level (0 a 6 Estrelas)
- **Escala de Notoriedade:** Cometa crimes (roubo de viaturas, carjacking na frente de policiais) ou atinja metas de entrega de contratos para aumentar as estrelas de procurado.
- **IA Tática de Perseguição:** Viaturas com sirene e giroflex funcional, velocidade proporcional ao veículo do jogador e manobras agressivas de interceptação (*Lead Pursuit*).
- **Desmanche como Refúgio:** Concluir entregas em desmanches despista viaturas próximas e reduz o nível de procurado.
- **Condições de Game Over:** Fim do tempo no relógio, destruição do veículo ocupado ou prisão física pela polícia (*Busted*).

### 5. Áudio Arcade Sintetizado & Top-Down
- **Motores com Caixa de 4 Marchas:** Áudio com modulação de pitch e rotação responsiva ao acelerador e marcha à ré.
- **Sirene Policial Espacializada:** Áudio de sirene com atenuação top-down no plano XZ e efeito Doppler.
- **Rádio Policial VHF:** Transmissões e alertas de despacho reproduzidos sempre que a notoriedade do jogador sobe.

---

## 🗺️ O Mapa: Bairro da Lapa (Rio de Janeiro)

A malha viária e os cenários foram construídos reproduzindo o traçado modular do icônico bairro da Lapa em um grid de `10m x 10m` por tile, com diversos pontos de referência:
- **Arcos da Lapa:** Aqueduto monumental modelado em dois níveis de pistas.
- **Catedral de São Sebastião**
- **Circo Voador & Fundição Progresso**
- **Escadaria Selarón**
- **Armazém do Senado, Bar das Quengas, Carioca da Gema e Lapa 40°**

---

## ⌨️ Controles

| Ação | Teclado | Gamepad |
| :--- | :--- | :--- |
| **Mover a Pé** | `W`, `A`, `S`, `D` ou `Setas` | Analógico Esquerdo |
| **Acelerar Veículo** | `W` ou `Seta Cima` | Gatilho Direito / `RT` / `R2` |
| **Frear / Marcha à Ré** | `S` ou `Seta Baixo` | Gatilho Esquerdo / `LT` / `L2` |
| **Virar Veículo** | `A` / `D` ou `Setas Laterais` | Analógico Esquerdo |
| **Entrar / Sair do Carro** | `E`, `F` ou `Enter` | Botão Superior (`Y` / `Triângulo`) |
| **Reiniciar (Game Over)** | `Enter` ou `R` | Botão Sul (`A` / `X`) |

---

## 🏗️ Arquitetura de Software & Design Patterns

O projeto segue boas práticas de engenharia e desacoplamento na Unity:
- **State Pattern:** Separação estrita entre `PlayerController` (a pé) e `VehicleController` (ao volante), ativando/desativando controladores para performance sem condicionais redundantes no `Update`.
- **Event-Driven Architecture:** Uso de `System.Action` e canais de eventos C# para orquestrar missões, polícia, áudio e HUD sem chamadas a `FindObjectOfType` em tempo de execução.
- **Data-Driven Design:** Configurações de carros (`VehicleData`) e missões (`VehicleContractData`) centralizadas em `ScriptableObjects`.
- **Editor Automation:** Menu `Tools/GTA2/*` com utilitários para gerar prefabs, sincronizar dados, criar sons em runtime e reconfigurar a cena em um clique.

---

## 🚀 Como Executar o Projeto

### Pré-requisitos
- **Unity Hub** com versão **Unity 6 (6000.x)** ou **Unity LTS 2022.3+**.
- Módulos padrão para sua plataforma de preferência (Windows, Linux ou macOS).

### Passo a Passo
1. Clone o repositório:
   ```bash
   git clone https://github.com/SEU_USUARIO/gta2-clone.git
   ```
2. Abra o **Unity Hub**, clique em **Add > Add project from disk** e selecione a pasta raiz clonada.
3. Certifique-se de que a versão correta do editor está selecionada e abra o projeto.
4. No Unity Editor, navegue até:
   ```
   Assets > Scenes > SampleScene.unity
   ```
5. Pressione **Play** ▶️ e divirta-se!

---

## 📁 Estrutura de Pastas

```
Assets/
├── Audio/            # Efeitos sonoros e loops de motor (.wav procedurais)
├── Data/             # ScriptableObjects de Veículos e Contratos
├── Materials/        # Materiais URP estilizados
├── Models/           # Kits 3D Low-Poly (Car kit, City-kit, Roads, Characters)
├── Prefabs/          # Prefabs prontos (Veículos, HUD, Sistema Policial)
├── Scenes/           # Cena principal do jogo (SampleScene.unity)
└── Scripts/
    ├── Audio/        # Síntese e reprodução de motor, sirene e rádio
    ├── Camera/       # Câmera zenital suave com zoom dinâmico
    ├── Editor/       # Gerador procedural da Lapa e ferramentas de Setup
    ├── Missions/     # Orquestrador do loop de missões e entregas
    ├── Player/       # Movimentação e interação a pé
    ├── Police/       # IA policial e gerenciamento de procurado
    ├── Traffic/      # Grafo viário, semáforos e IA de tráfego urbano
    ├── UI/           # HUDs de missões, veículos e Game Over
    └── Vehicles/     # Física veicular, saúde e danos
```

---

## 📄 Licença

Este projeto é disponibilizado sob a licença [MIT](LICENSE). Sinta-se livre para usar, estudar, modificar e distribuir.

