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

3. **Veículos (`VehicleController` & `VehicleData`):**
   - **Dados do Veículo (`VehicleData` ScriptableObject):**
     - Cada um dos 20 veículos do pacote `Car kit` (excluindo karts) possui um asset `VehicleData` em `Assets/Data/Vehicles/` e um Prefab pronto em `Assets/Prefabs/Vehicles/`.
     - Armazena o nome do veículo (`vehicleName`), configurações de **Drive Settings** (`acceleration`, `reverseAcceleration`, `maxForwardSpeed`, `maxReverseSpeed`, `brakeStrength`, `naturalDrag`) e **Steering & Grip** (`turnSpeed`, `minSpeedForFullTurn`, `lateralFriction`).
     - O `VehicleController` sincroniza automaticamente seus atributos com o `VehicleData` atribuído via `ApplyVehicleData()` no `Awake()` e `OnValidate()`.
   - Física arcade top-down estilo GTA 2 calculada no `FixedUpdate()`:
     - **Aceleração (`W` / `Seta para Cima`):** Acelera no sentido frontal do veículo (`transform.forward`) até `maxForwardSpeed`. Se estiver em marcha à ré, freia primeiro antes de avançar.
     - **Freio e Ré (`S` / `Seta para Baixo`):** Aplica frenagem (`brakeStrength`) enquanto o carro se move para frente; ao parar completamente, engata a marcha à ré até `maxReverseSpeed`.
     - **Direção e Aderência (`A` / `D` ou Setas Horizontais):** Faz a curva proporcionalmente à velocidade atual (invertendo o giro na ré) e amortece o deslizamento lateral (`lateralFriction`) para simular aderência dos pneus com leve drift.
     - **Material Físico Zero-Friction:** O `BoxCollider` do veículo recebe um `PhysicsMaterial` sem atrito (`dynamicFriction = 0`, `staticFriction = 0`, `Minimum`) em `Awake()`, evitando que o atrito nativo do PhysX com o piso ou paredes trave o chassi ou reduza a aceleração arcade.
   - **Visual das Rodas:**
     - As 4 rodas (`wheel-front-left`, `wheel-front-right`, `wheel-back-left`, `wheel-back-right`) recebem pivots centralizados e alinhados ao chassi em `Awake()`.
     - As rodas dianteiras esterçam suavemente no eixo Y (`maxVisualSteerAngle`) e todas as 4 rodas giram no eixo X proporcionalmente à velocidade linear e ao raio da roda (`wheelRadius`).
   - **Entrada, Saída e Controle de IA:**
     - Tecla de interação (`StealVehicle`: `E`, `F`, `Enter` ou `Gamepad Button North`).
     - Saída prioritária pela porta esquerda (`ExitPointLeft`), com verificação de colisão (`Physics.OverlapSphere`) e fallback automático para a porta direita (`ExitPointRight`) caso haja obstáculo.
     - Suporta condução autônoma por IA (`SetAIInput` / `ClearAIInput` / `HasAIDriver`) utilizando a mesma física arcade e animação de rodas no `FixedUpdate()`. Quando o jogador rouba um veículo (`EnterVehicle`), o controle da IA é limpo automaticamente e `OnDriverEntered` notifica o `TrafficVehicleAI`.

4. **Mapa Urbano — Bairro da Lapa (`LapaMapGenerator`):**
   - A `SampleScene` reproduz o traçado viário e os pontos turísticos da Lapa (Rio de Janeiro) em um grid modular de `10m x 10m` por tile (`RoadScale = (10, 3, 10)`, `BuildingScale = 8.5`).
   - **Malha Viária (`Roads`):** Eixos horizontais (*R. Fr. Caneca*, *Av. Visc. do Rio Branco*, *R. do Senado*, *Av. Henrique Valadares / Av. República do Chile*, *R. do Rezende*, *R. dos Arcos*, *R. Evaristo da Veiga*, *Av. Mem de Sá*, *R. Riachuelo / R. Teixeira de Freitas*) e eixos verticais (*R. Ubaldino do Amaral*, *R. dos Inválidos*, *R. Gomes Freire / R. Francisco Muratori*, *R. Sílvio Romero*, *R. do Lavradio / R. Joaquim Murtinho*, *Ld. Santa Teresa*, *Av. República do Paraguai / R. Joaquim Silva*, *R. das Marrecas / R. da Lapa*, *R. Sen. Dantas / R. do Passeio / R. Augusto Severo*).
   - **Pontos de Referência (*Landmarks*):** *Arcos da Lapa* (aqueduto em dois níveis com `road-bridge` e `bridge-pillar-wide`), *Catedral de São Sebastião*, *Circo Voador*, *Armazém do Senado*, *Bar das Quengas*, *Carioca da Gema*, *Lapa 40°* e *Escadaria Selarón*.
   - **Física do Mapa:** O plano base `Ground` (`340m x 320m`) garante rolagem contínua sem costuras para os veículos, enquanto os prédios (`City-kit`), arcos e estruturas sólidas recebem `BoxCollider` ajustado às suas `Bounds` (sem obstruir a pista com colisores de postes curvos ou tiles de rua).

5. **Sistema de Tráfego Urbano (`TrafficManager`, `TrafficVehicleAI`, `TrafficIntersection`, `TrafficRoadGraph`, `TrafficWaypoint`):**
   - **Grafo de Faixas (`TrafficRoadGraph` & `TrafficWaypoint`):**
     - Gerado automaticamente pelo `LapaMapGenerator` dentro de `LapaMap/Traffic_Network`, sem qualquer busca em runtime.
     - Cada tile viário possui pares de `TrafficWaypoint` (entrada e saída) deslocados `1.85m` para a direita do sentido de rolamento (mão dupla), conectados entre tiles adjacentes e dentro dos cruzamentos (seguir reto, dobrar à direita/esquerda ou retorno em fim de rua).
   - **Semáforos e Controle de Cruzamentos (`TrafficIntersection`):**
     - Todos os cruzamentos de 3 ou 4 vias (`neighborCount >= 3`) possuem um controlador `TrafficIntersection` que alterna ciclos de sinal **Verde** (`6.5s`) e **Amarelo/Limpeza** (`2.2s`) entre o eixo Horizontal (Leste-Oeste) e o eixo Vertical (Norte-Sul), com *offset* de fase por coordenada para criar ondas verdes.
     - Cada via de entrada real do cruzamento recebe um poste `traffic-light.fbx` na esquina correspondente, com um ponto de iluminação `Point Light` sem sombras e uma micro-lente emissiva (`0.14m`) acoplados diretamente ao topo do modelo 3D (`Bounds`).
     - Controla a reserva da área central do cruzamento (`CanVehicleEnter`, `NotifyVehicleEntered`, `NotifyVehicleExited`) registrando a direção de entrada (`entryDirections`) e expiração automática por tempo ou estol (`maxReservationSeconds`), impedindo que veículos em direções opostas ou transversais entrem simultaneamente e colidam ao dobrar à esquerda.
   - **Condução Autônoma, Curvas Bézier e Anti-Deadlock (`TrafficVehicleAI`):**
     - Navega com **Pure-Pursuit** nos trechos retos (mantendo o veículo centralizado na faixa) e **Curvas de Bézier Quadráticas** pelo vértice ortogonal das faixas em curvas de 90° e retornos de 180°, evitando cortar esquinas sobre a contramão ou subir nas calçadas.
     - Respeita a linha de retenção (`WP_In_*`) dos semáforos (`TrafficIntersection`), parando antes da faixa no sinal vermelho/amarelo (`IsWaitingAtRedLight`) e concluindo a travessia caso já tenha ultrapassado a linha de retenção.
     - Sensor frontal sem ponto cego no `FixedUpdate()`: combina `Physics.OverlapSphereNonAlloc` imediato no para-choque frontal + `Physics.SphereCastNonAlloc` partindo do interior do chassi + sensor angulado em curvas, filtrando veículos na mão contrária pelo deslocamento lateral (`localOtherPos.x`).
     - Sistema **Anti-Deadlock com rastreamento de deslocamento real (`stuckAnchorPosition`)**: evita loops infinitos de frente-e-ré verificando espaço traseiro (`IsRearBlocked`), aplicando desobstrução multi-colisor (`ActivatePassThrough` via `OverlapSphereNonAlloc`) e realinhamento de segurança (`SnapToCurrentLane`) caso o bloqueio persista.
     - Escuta `VehicleController.OnDriverEntered`: ao ser roubado pelo jogador (*carjacking*), restaura colisões, libera reservas de cruzamento, desativa a IA imediatamente e dispara `OnStolenByPlayer`.
   - **Gerenciamento de População (`TrafficManager`):**
     - Mantém até `maxActiveVehicles` (`16`) circulando ao redor do jogador/veículo atual e incorpora automaticamente veículos iniciais posicionados na cena (`initialSceneVehicles`) ao fluxo de tráfego no `Start()`.
     - Realiza spawn preferencialmente em trechos retos/saídas de cruzamento desobstruídos (`OverlapSphereNonAlloc`) no anel externo à câmera (`36m` a `95m`) e despawna veículos de IA que ultrapassam `despawnDistance` (`115m`).
     - Veículos roubados pelo jogador são removidos da lista de tráfego de IA para nunca desaparecerem enquanto estiverem em uso.

6. **Missões & Entrega (Próximas Etapas):**
   - `MissionManager`: Sorteia demandas e gerencia recompensas.
   - `DeliveryZone`: Trigger isolado que apenas valida o veículo, dispara `OnVehicleDelivered` e delega a lógica econômica.

---

## Estrutura de Pastas Atual

- `Assets/Scripts/Player/PlayerController.cs` — Movimentação omnidirecional a pé, animações (`Idle`/`Run`) e detecção/entrada em veículos próximos (`OverlapSphereNonAlloc`).
- `Assets/Scripts/Vehicles/VehicleController.cs` — Física veicular arcade, controle de direção/freio/ré (jogador e IA), animação procedural das 4 rodas, leitura de `VehicleData` e lógica de entrada/saída.
- `Assets/Scripts/Vehicles/VehicleData.cs` — `ScriptableObject` que armazena o nome (`vehicleName`), parâmetros de condução (`Drive Settings`) e direção/aderência (`Steering & Grip`) de cada veículo.
- `Assets/Scripts/Traffic/TrafficWaypoint.cs` — Nó direcional de faixa de rolamento com conexões serializadas para os próximos waypoints, vínculo opcional com semáforo (`TrafficIntersection`) e visualização via Gizmos.
- `Assets/Scripts/Traffic/TrafficIntersection.cs` — Controlador de semáforo e reserva de cruzamento (ciclos Verde/Amarelo/Vermelho por eixo Horizontal e Vertical + atualização visual de lâmpadas emissivas).
- `Assets/Scripts/Traffic/TrafficRoadGraph.cs` — Grafo viário serializado que armazena todos os `TrafficWaypoint`s da Lapa e fornece pontos de spawn livres de obstáculos (`OverlapSphereNonAlloc`).
- `Assets/Scripts/Traffic/TrafficVehicleAI.cs` — Motorista autônomo de IA que navega pelo grafo de faixas, freia diante de trânsito/pedestres, executa manobra de desatolamento e cede o controle quando roubado pelo jogador.
- `Assets/Scripts/Traffic/TrafficManager.cs` — Gerenciador de densidade de tráfego que instancia e recicla veículos autônomos em um anel ao redor do jogador.
- `Assets/Scripts/UI/VehicleHudUI.cs` — HUD desacoplado que escuta `OnVehicleEntered` / `OnVehicleExited` do `PlayerController` e exibe o nome do veículo (`VehicleName`) com efeito de fade no canto inferior direito da tela.
- `Assets/Scripts/Camera/TopDownCameraFollow.cs` — Câmera top-down suave que alterna o alvo (`SetTarget`) entre o jogador e o veículo atual, aplicando afastamento/aproximação vertical dinâmico (`maxSpeedExtraHeight`, `zoomOutSmoothTime`, `zoomInSmoothTime`) proporcional à velocidade normalizada (`NormalizedSpeed`) do veículo.
- `Assets/Scripts/Editor/LapaMapGenerator.cs` — Gerador procedural/modular de Editor (`Tools/GTA2/Build Lapa Map`) que constrói o mapa da Lapa e o grafo de tráfego (`Traffic_Network`) na `SampleScene` usando os kits `Roads` e `City-kit`.
- `Assets/Scripts/Editor/SceneSetupUtility.cs` — Utilitário de Editor (`Tools/GTA2/Generate All Vehicle Data & Prefabs`, `Tools/GTA2/Setup Player Scene`, `Tools/GTA2/Spawn Test Vehicles`, `Tools/GTA2/Setup Vehicle HUD` e `Tools/GTA2/Setup Traffic System`) para configurar o mapa da Lapa, materiais URP, jogador, tráfego urbano, HUD e gerar/instanciar os 20 `VehicleData` e 20 Prefabs de veículos.
- `Assets/Data/Vehicles/` — 20 `ScriptableObject`s (`VehicleData_*.asset`) com atributos balanceados por categoria para todos os veículos do `Car kit` (exceto karts).
- `Assets/Prefabs/Vehicles/` — 20 Prefabs completos (`Vehicle_*.prefab`) configurados com `Rigidbody`, `BoxCollider`, `VehicleController`, `TrafficVehicleAI`, `VehicleData`, pontos de saída e referências das 4 rodas.
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
