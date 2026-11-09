# Projeto: GTA 2 Clone (Unity)

## Visão Geral
Clone top-down inspirado no clássico GTA 2 focado exclusivamente no loop central de roubo de veículos e entrega em pontos designados (*Drop-off Zones*). 
Estética: Low-Poly 3D estilizado com câmera zenital de baixa distorção (FOV 25°-35°).

---

## Stack Tecnológica & Pacotes
- **Engine:** Unity (LTS 2022.3+ ou Unity 6)
- **Linguagem:** C# (.NET Standard 2.1)
- **Render Pipeline:** Universal Render Pipeline (URP)
- **Input:** New Input System (`com.unity.inputsystem`) — `Assets/InputSystem_Actions.inputactions`
- **Câmera:** `TopDownCameraFollow` (zenital 90° com `SmoothDamp` e zoom dinâmico proporcional à velocidade do veículo estilo GTA 2) / Cinemachine (`com.unity.cinemachine`)
- **Física:** Rigidbody 3D com eixos restritos (`FreezeRotationX/Z`) para veículos e `CharacterController` para o jogador a pé

---

## Arquitetura de Software & Padrões

1. **Controle do Jogador e Transição de Estado (A Pé / Dirigindo):**
   - `PlayerController` controla apenas a movimentação a pé (`CharacterController`) e fica desativado (`enabled = false`) enquanto o jogador está dirigindo, evitando condicionais gigantes no `Update()`.
   - O GameObject do jogador **NÃO** é destruído ao entrar no carro: seu `CharacterController` e `visualRoot` são desativados e ele se torna filho (`transform.SetParent`) do veículo.

2. **Desacoplamento e Eventos:**
   - Comunicação entre veículos, jogador e missões deve ser via C# Events/Actions (`System.Action`) ou ScriptableObject Event Channels (`OnVehicleEntered`, `OnVehicleExited`, `OnDriverEntered`, `OnDriverExited`).
   - NUNCA use `GameObject.Find()`, `FindObjectOfType()`, ou `SendMessage()` em runtime. Injeção de dependência via Inspector ou referências gerenciadas.

3. **Veículos (`VehicleController`):**
   - Física arcade top-down estilo GTA 2 calculada no `FixedUpdate()`:
     - **Aceleração (`W` / `Seta para Cima`):** Acelera no sentido frontal do veículo (`transform.forward`) até `maxForwardSpeed`. Se estiver em marcha à ré, freia primeiro antes de avançar.
     - **Freio e Ré (`S` / `Seta para Baixo`):** Aplica frenagem (`brakeStrength`) enquanto o carro se move para frente; ao parar completamente, engata a marcha à ré até `maxReverseSpeed`.
     - **Direção e Aderência (`A` / `D` ou Setas Horizontais):** Faz a curva proporcionalmente à velocidade atual (invertendo o giro na ré) e amortece o deslizamento lateral (`lateralFriction`) para simular aderência dos pneus com leve drift.
   - **Visual das Rodas:**
     - As 4 rodas (`wheel-front-left`, `wheel-front-right`, `wheel-back-left`, `wheel-back-right`) recebem pivots centralizados e alinhados ao chassi em `Awake()`.
     - As rodas dianteiras esterçam suavemente no eixo Y (`maxVisualSteerAngle`) e todas as 4 rodas giram no eixo X proporcionalmente à velocidade linear e ao raio da roda (`wheelRadius`).
   - **Entrada e Saída:**
     - Tecla de interação (`StealVehicle`: `E`, `F`, `Enter` ou `Gamepad Button North`).
     - Saída prioritária pela porta esquerda (`ExitPointLeft`), com verificação de colisão (`Physics.OverlapSphere`) e fallback automático para a porta direita (`ExitPointRight`) caso haja obstáculo.

4. **Mapa Urbano — Bairro da Lapa (`LapaMapGenerator`):**
   - A `SampleScene` reproduz o traçado viário e os pontos turísticos da Lapa (Rio de Janeiro) em um grid modular de `10m x 10m` por tile (`RoadScale = (10, 3, 10)`, `BuildingScale = 8.5`).
   - **Malha Viária (`Roads`):** Eixos horizontais (*R. Fr. Caneca*, *Av. Visc. do Rio Branco*, *R. do Senado*, *Av. Henrique Valadares / Av. República do Chile*, *R. do Rezende*, *R. dos Arcos*, *R. Evaristo da Veiga*, *Av. Mem de Sá*, *R. Riachuelo / R. Teixeira de Freitas*) e eixos verticais (*R. Ubaldino do Amaral*, *R. dos Inválidos*, *R. Gomes Freire / R. Francisco Muratori*, *R. Sílvio Romero*, *R. do Lavradio / R. Joaquim Murtinho*, *Ld. Santa Teresa*, *Av. República do Paraguai / R. Joaquim Silva*, *R. das Marrecas / R. da Lapa*, *R. Sen. Dantas / R. do Passeio / R. Augusto Severo*).
   - **Pontos de Referência (*Landmarks*):** *Arcos da Lapa* (aqueduto em dois níveis com `road-bridge` e `bridge-pillar-wide`), *Catedral de São Sebastião*, *Circo Voador*, *Armazém do Senado*, *Bar das Quengas*, *Carioca da Gema*, *Lapa 40°* e *Escadaria Selarón*.
   - **Física do Mapa:** O plano base `Ground` (`340m x 320m`) garante rolagem contínua sem costuras para os veículos, enquanto todos os prédios (`City-kit`), pilares, cercas e barreiras de fim de pista recebem `BoxCollider` ajustado às suas `Bounds`.

5. **Missões & Entrega (Próximas Etapas):**
   - `MissionManager`: Sorteia demandas e gerencia recompensas.
   - `DeliveryZone`: Trigger isolado que apenas valida o veículo, dispara `OnVehicleDelivered` e delega a lógica econômica.

---

## Estrutura de Pastas Atual

- `Assets/Scripts/Player/PlayerController.cs` — Movimentação omnidirecional a pé, animações (`Idle`/`Run`) e detecção/entrada em veículos próximos (`OverlapSphereNonAlloc`).
- `Assets/Scripts/Vehicles/VehicleController.cs` — Física veicular arcade, controle de direção/freio/ré, animação procedural das 4 rodas e lógica de entrada/saída.
- `Assets/Scripts/Camera/TopDownCameraFollow.cs` — Câmera top-down suave que alterna o alvo (`SetTarget`) entre o jogador e o veículo atual, aplicando afastamento/aproximação vertical dinâmico (`maxSpeedExtraHeight`, `zoomOutSmoothTime`, `zoomInSmoothTime`) proporcional à velocidade normalizada (`NormalizedSpeed`) do veículo.
- `Assets/Scripts/Editor/LapaMapGenerator.cs` — Gerador procedural/modular de Editor (`Tools/GTA2/Build Lapa Map`) que constrói o mapa da Lapa na `SampleScene` usando os kits `Roads` e `City-kit`.
- `Assets/Scripts/Editor/SceneSetupUtility.cs` — Utilitário de Editor (`Tools/GTA2/Setup Player Scene` e `Tools/GTA2/Spawn Test Vehicles`) para configurar o mapa da Lapa, materiais URP, jogador e veículos de teste (`Vehicle_SedanSports`, `Vehicle_Taxi`, `Vehicle_Police`).
- `Assets/Models/` — Kits 3D Low-Poly (`Characters`, `Car kit`, `City-kit`, `Roads`).
- `Assets/Materials/` — Materiais URP (`PlayerSkin.mat`, `Ground.mat`, `CarColormap.mat`, `RoadsColormap.mat`, `CityColormap.mat`).
- `Assets/Animations/` — `PlayerAnimator.controller`.

---

## Diretrizes de Código C# para a IA

- Use C# moderno: pattern matching, `var` quando o tipo for explícito, métodos com expressão de corpo (`=>`) para propriedades simples.
- Variáveis privadas serializáveis: `[SerializeField] private float speed;`.
- Métodos de ciclo de vida (`Awake`, `Start`, `Update`, `FixedUpdate`) devem ser privados ou protegidos.
- Sempre calcule física veicular dentro do `FixedUpdate()`.
- Inclua documentação XML (`/// <summary>`) em métodos públicos de eventos e controladores.
