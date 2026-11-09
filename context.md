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
   - Comunicação entre veículos, jogador, missões e polícia deve ser via C# Events/Actions (`System.Action`) ou ScriptableObject Event Channels (`OnVehicleEntered`, `OnVehicleExited`, `OnDriverEntered`, `OnDriverExited`, `OnHealthChanged`, `OnVehicleExploded`, `OnWantedStarsChanged`).
   - NUNCA use `GameObject.Find()`, `FindObjectOfType()`, ou `SendMessage()` em runtime. Injeção de dependência via Inspector ou referências gerenciadas.

3. **Veículos (`VehicleController`, `VehicleData` & `VehicleHealth`):**
   - **Dados do Veículo (`VehicleData` ScriptableObject):**
     - Cada um dos 20 veículos do pacote `Car kit` (excluindo karts) possui um asset `VehicleData` em `Assets/Data/Vehicles/` e um Prefab pronto em `Assets/Prefabs/Vehicles/`.
     - Armazena o nome do veículo (`vehicleName`), configurações de **Drive Settings** (`acceleration`, `reverseAcceleration`, `maxForwardSpeed`, `maxReverseSpeed`, `brakeStrength`, `naturalDrag`), **Steering & Grip** (`turnSpeed`, `minSpeedForFullTurn`, `lateralFriction`) e **Durability & Health** (`maxHealth`, balanceado por categoria: esportivos `95`, sedans `115`, SUVs/vans/viaturas `135` e caminhões/tratores `175`).
     - O `VehicleController` sincroniza automaticamente seus atributos com o `VehicleData` atribuído via `ApplyVehicleData()` no `Awake()` e `OnValidate()`.
   - **Física arcade top-down estilo GTA 2 calculada no `FixedUpdate()`:**
     - **Aceleração (`W` / `Seta para Cima`):** Acelera no sentido frontal do veículo (`transform.forward`) até `maxForwardSpeed`. Se estiver em marcha à ré, freia primeiro antes de avançar.
     - **Freio e Ré (`S` / `Seta para Baixo`):** Aplica frenagem (`brakeStrength`) enquanto o carro se move para frente; ao parar completamente, engata a marcha à ré até `maxReverseSpeed`.
     - **Direção e Aderência (`A` / `D` ou Setas Horizontais):** Faz a curva proporcionalmente à velocidade atual (invertendo o giro na ré, com esterçamento de baixa velocidade otimizado) e amortece o deslizamento lateral através de um modelo híbrido de aderência (amortecimento viscoso proporcional `lateralFriction * 2.8` + atrito linear direto `lateralFriction * 1.8`, com assistência extra de aderência na frenagem e zeramento de `rb.angularVelocity.y` contra colisões indesejadas), eliminando o efeito de derrapagem excessiva/gelo e garantindo direção firme, ágil e com leve drift arcade autêntico estilo GTA 2.
     - **Material Físico Zero-Friction:** O `BoxCollider` do veículo recebe um `PhysicsMaterial` sem atrito (`dynamicFriction = 0`, `staticFriction = 0`, `Minimum`) em `Awake()`, evitando que o atrito nativo do PhysX com o piso ou paredes trave o chassi ou reduza a aceleração arcade.
   - **Visual das Rodas:**
     - As 4 rodas (`wheel-front-left`, `wheel-front-right`, `wheel-back-left`, `wheel-back-right`) recebem pivots centralizados e alinhados ao chassi em `Awake()`.
     - As rodas dianteiras esterçam suavemente no eixo Y (`maxVisualSteerAngle`) e todas as 4 rodas giram no eixo X proporcionalmente à velocidade linear e ao raio da roda (`wheelRadius`).
   - **Entrada, Saída e Controle de IA:**
     - Tecla de interação (`StealVehicle`: `E`, `F`, `Enter` ou `Gamepad Button North`).
     - Registra `WasMovingOrAICarjackedOnLastEntry` no momento do roubo (antes de limpar a IA) e diferencia saída manual do jogador (`ExitVehicle` -> `LastExitWasForced = false`) de ejeção segura do sistema (`ForceExitVehicle` -> `LastExitWasForced = true`).
     - Saída prioritária pela porta esquerda (`ExitPointLeft`), com verificação de colisão (`Physics.OverlapSphere`) e fallback automático para a porta direita (`ExitPointRight`) caso haja obstáculo.
     - Suporta condução autônoma por IA (`SetAIInput` / `ClearAIInput` / `SetAISpeedMultiplier` / `HasAIDriver`) utilizando a mesma física arcade e animação de rodas no `FixedUpdate()`.
   - **Sistema de Dano e Explosão Veicular (`VehicleHealth`):**
     - Computa dano por impacto físico em `OnCollisionEnter` proporcional à velocidade de aproximação relativa acima de `minImpactSpeed` (`3.2 m/s`), aplicando multiplicador extra quando abalroado por viaturas policiais.
     - Exibe feedback visual progressivo no capô: fumaça contínua abaixo de `55%` da vida e chamas com iluminação dinâmica (`Point Light`) abaixo de `28%`.
     - Quando a vida chega a `0`, o veículo explode (`Explode()`): carboniza o chassi (`MaterialPropertyBlock`), aplica impulso físico vertical/rotacional, instancia efeito procedural de bola de fogo + fumaça (`ExplosionVisualRunner`) e dispara `OnVehicleExploded` / `OnAnyVehicleExploded`. Se o jogador estiver dentro do veículo quando ele explode, ocorre **Game Over imediato (`GameOverReason.VehicleDestroyed`)**.

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
     - Protege o veículo alvo atual da missão (`SetProtectedMissionVehicle`) contra despawn por distância e expõe `TryGetOrSpawnTrafficTarget` e `UnregisterVehicle` para integração direta com o `MissionManager`.

6. **Sistema de Missões Infinito — Arcade Loop (`MissionManager`, `VehicleContractData`, `DeliveryZone`, `MissionTargetIndicator`, `MissionEvents`):**
   - **Contratos (`VehicleContractData` ScriptableObject):**
     - 20 assets em `Assets/Data/Contracts/Contract_*.asset` associados aos 20 veículos do jogo, definindo `baseReward`, `targetDeliveryTimeSeconds`, `maxSpeedBonus`, `baseTimeAwardSeconds` (tempo adicionado ao relógio global), `heatOnSteal`, `heatOnDelivery` e `allowTrafficSpawn`.
   - **Banco de Tempo Global e Game Over (*Estilo Crazy Taxi*):**
     - A partida inicia com `initialTimeBankSeconds` (`60s`, até o teto `maxTimeBankSeconds = 120s`) que decresce continuamente em `MissionManager.Update()`, emitindo `OnTimerUpdated`.
     - A cada entrega concluída, o jogador ganha tempo extra (`CalculateDeliveryTimeAward`) com **escalada de dificuldade progressiva** (`timeAwardDecayPerDelivery = 1.5s` a menos por entrega realizada até o piso `minTimeAwardPerDelivery = 11s`).
     - O `MissionManager` encerra a partida via `TriggerCustomGameOver(GameOverReason reason, string customMessage)` para os 3 motivos de derrota (`GameOverReason.TimeExpired`, `GameOverReason.Busted` e `GameOverReason.VehicleDestroyed`), ejetando/travando o jogador e abrindo a tela de **Game Over** com resumo da sessão e reinício imediato (`RestartGame()` via botão ou teclas `Enter` / `R`).
   - **Sorteio e Regras de Spawn (`MissionManager`):**
     - **1ª Missão (`currentMissionNumber == 1`):** Sempre spawna um veículo parado (`TrafficVehicleAI.enabled = false`) no ponto `FirstMissionSpawnPoint` imediatamente próximo ao jogador.
     - **Missões Subsequentes:** Sorteia um contrato e alterna entre spawnar o veículo parado em um dos 12 `vehicleSpawnPoints` espalhados pelo mapa da Lapa ou selecionar/instanciar o veículo em movimento no tráfego urbano (`TrafficManager.TryGetOrSpawnTrafficTarget`).
   - **Seta Direcional Orbital 3D (`MissionTargetIndicator`):**
     - Orbita o jogador (ou o veículo conduzido pelo jogador) no plano XZ com malha 3D procedural (`Assets/Data/MissionArrowMesh.asset`) e efeito de oscilação radial (*bobbing*).
     - Aponta para o veículo alvo na **cor verde** (`MissionPhase.StealVehicle`) e muda automaticamente para a **cor amarela** (`MissionPhase.DeliverVehicle`) apontando para a `DeliveryZone` ativa assim que o jogador entra no carro alvo (voltando para verde no carro caso o jogador saia dele antes de concluir a entrega).
   - **Zonas de Desmanche / Entrega (`DeliveryZone`):**
     - 4 zonas distribuídas pelo mapa da Lapa (*Desmanche Arcos da Lapa*, *Desmanche Armazém do Senado*, *Desmanche Largo do Passeio* e *Desmanche Mem de Sá Oeste*) com `BoxCollider` trigger, marcação visual pulsante amarela no piso (`VisualHighlight`) e ponto seguro de ejeção na calçada (`SafeEjectPoint`).
     - Ao detectar a entrada do veículo alvo conduzido pelo jogador, dispara `OnVehicleDelivered`: o `MissionManager` computa a recompensa + bônus de velocidade + tempo extra no relógio, ejeta o jogador em segurança via `VehicleController.ForceExitVehicle(safePos, safeRot)`, destrói o veículo entregue e inicia o próximo contrato imediatamente.

7. **Sistema de Polícia — Wanted Level (0 a 6 Estrelas) & Perseguição (`PoliceManager`, `PoliceVehicleAI`):**
   - **Gatilhos de Aumento e Redução de Procurado (`WantedStars` de `0` a `6`):**
     - **A cada 3 entregas (`CompletedMissionsCount % 3 == 0`):** aumenta automaticamente o nível de procurado (escalando a notoriedade até 6 estrelas).
     - **Desmanche Esconde o Jogador (Demais Entregas):** ao entregar o veículo na `DeliveryZone` em entregas que não são múltiplas de 3, o Desmanche dispersa as viaturas próximas e **reduz `-1 estrela`** no nível de procurado.
     - **Roubo de carro em movimento perto da polícia:** se o jogador roubar um veículo em movimento (`WasMovingOrAICarjackedOnLastEntry == true`) dentro do raio de visão de uma viatura policial (`policeWitnessRadius = 24m` com verificação de linha de visão livre de prédios) ou roubar uma viatura policial em movimento, o nível de procurado sobe **+1 estrela** imediatamente.
   - **Patrulha e Escalonamento de Viaturas (`PoliceManager`):**
     - Em **0 estrelas**: mantém `patrolUnitsAtZeroStars = 2` viaturas circulando em modo `Patrol` nas redondezas para fiscalizar as ruas.
     - De **1 a 6 estrelas**: ativa o modo `Pursuit` em todas as unidades policiais e escala a quantidade de viaturas perseguidoras conforme o nível de estrelas (`1★` = 1 viatura até `6★` = 6 viaturas simultâneas, incluindo viaturas pesadas `Vehicle_TractorPolice` a partir de 4 estrelas).
   - **IA de Perseguição Progressiva e Giroflex (`PoliceVehicleAI`):**
     - Em modo `Pursuit`, ativa o giroflex 3D no teto (lentes emissivas vermelha/azul alternadas + `Point Light`), ignora semáforos vermelhos e calcula sua velocidade máxima **proporcionalmente à velocidade máxima do veículo atual do jogador** (`RelativeSpeedRatioByStar`: `1★` = 76%, `2★` = 84%, `3★` = 92%, `4★` = 98%, `5★` = 104%, `6★` = 110%).
     - Em `1★` e `2★`, as viaturas são mais lentas que o carro do jogador e moderam o acelerador ao se aproximar (`< 7.5m`), permitindo escapar nas retas e curvas; de `3★` a `6★`, utilizam *Lead Pursuit* e interceptação agressiva.
   - **Condição de Derrota Policial por Captura (*Busted / Preso*):**
     - O jogador **pode sair livremente do carro** a qualquer momento para trocar de veículo (ex: pegar o carro da missão ou fugir a pé), mesmo durante uma perseguição policial.
     - A derrota por prisão (`GameOverReason.Busted`) ocorre caso uma viatura policial perseguidora alcance e encoste nele enquanto estiver correndo a pé (`bustedOnFootContactRadius = 3.1m`).

8. **Sistema de Áudio Arcade Top-Down (`ProceduralAudioSynthesizer`, `VehicleEngineAudio`, `PoliceRadioAudio`, `PoliceVehicleAI`):**
   - **Síntese Procedural e Assets `.wav` (`ProceduralAudioSynthesizer` & `Assets/Audio/`):**
     - Gera tanto arquivos `.wav` PCM 16-bit (`44.1 kHz`) em `Assets/Audio/` via `SceneSetupUtility` quanto `AudioClip`s procedurais em memória como fallback sem dependências externas.
     - Loops de motor matematicamente contínuos (`2.0s` sem clique de transição) separados em 3 categorias sonoras (`EngineSoundCategory`: `Standard`, `Sports`, `Heavy`).
   - **Som de Motor dos Veículos (`VehicleEngineAudio`):**
     - Acoplado a todos os 20 Prefabs de veículos (`Vehicle_*.prefab`) e sincronizado ao `VehicleController`.
     - Quando conduzido pelo jogador (`IsOccupied`): reproduz em primeiro plano com simulação de **caixa de 4 marchas arcade estilo GTA 2** (`ComputeTargetPitch`), resposta imediata ao pedal de acelerador (`CurrentThrottleInput`) e timbre próprio de marcha à ré.
     - Quando conduzido por IA (`HasAIDriver`): calcula atenuação de distância e *stereo pan* (`panStereo`) no plano horizontal XZ em relação à câmera (`aiAudibleRadius = 28m`), pausando automaticamente o `AudioSource` quando o veículo está longe ou estacionado para economizar canais de áudio.
   - **Som de Sirene da Polícia (`PoliceVehicleAI` & `Assets/Audio/PoliceSiren_Loop.wav`):**
     - Ativado automaticamente junto ao giroflex luminoso quando a viatura entra em estado `Pursuit` (1 a 6 estrelas) e desligado em modo `Patrol` (0 estrelas), quando roubada ou quando destruída.
     - Utiliza atenuação planar XZ (`sirenAudibleRadius = 72m`), *stereo panning* direcional (esquerda/direita) e efeito Doppler sutil conforme a velocidade de aproximação da viatura em relação ao jogador, com *offset* de fase e *pitch* individual por unidade policial.
   - **Som de Rádio Policial ao Aumentar Estrela (`PoliceRadioAudio` & `Assets/Audio/PoliceRadio_StarIncrease.wav`):**
     - Componente desacoplado em `PoliceSystem` que escuta `PoliceManager.OnWantedStarsChanged`.
     - Sempre que `newStars > oldStars` (aumento de nível de procurado), reproduz a transmissão clássica de rádio VHF policial (*Squelch* de abertura + *Beep* duplo de alerta da central + formantes vocais de despacho filtrados em banda estreita + *Roger Beep* e *Squelch* final).

---

## Estrutura de Pastas Atual

- `Assets/Scripts/Audio/ProceduralAudioSynthesizer.cs` — Sintetizador procedural PCM (`44.1 kHz`) e gravador `.wav` para loops de motor (`Standard`, `Sports`, `Heavy`), sirene policial (*Wail*) e despacho de rádio VHF policial.
- `Assets/Scripts/Audio/VehicleEngineAudio.cs` — Controlador de áudio do motor dos veículos com curva de 4 marchas arcade, modulação de acelerador/ré e atenuação espacial top-down no plano XZ para tráfego de IA.
- `Assets/Scripts/Audio/PoliceRadioAudio.cs` — Receptor de eventos do `PoliceManager` que toca o som de despacho de rádio policial sempre que o jogador ganha estrelas de procurado.
- `Assets/Scripts/Player/PlayerController.cs` — Movimentação omnidirecional a pé, animações (`Idle`/`Run`) e detecção/entrada em veículos próximos (`OverlapSphereNonAlloc`).
- `Assets/Scripts/Vehicles/VehicleController.cs` — Física veicular arcade, controle de direção/freio/ré (jogador e IA), animação procedural das 4 rodas, leitura de `VehicleData`, rastreamento de roubo em movimento e lógica de entrada/saída (`ExitVehicle` e `ForceExitVehicle`).
- `Assets/Scripts/Vehicles/VehicleData.cs` — `ScriptableObject` que armazena o nome (`vehicleName`), parâmetros de condução (`Drive Settings`), direção/aderência (`Steering & Grip`) e vida máxima (`maxHealth`) de cada veículo.
- `Assets/Scripts/Vehicles/VehicleHealth.cs` — Componente de vida, dano por colisão física, efeitos visuais progressivos (fumaça e fogo no capô) e explosão do veículo quando a vida chega a zero.
- `Assets/Scripts/Police/PoliceManager.cs` — Gerenciador do nível de procurado (0 a 6 estrelas), gatilhos de crime (a cada 3 entregas ou roubo de carro em movimento perto da polícia), spawn escalonado de viaturas e condição de derrota por captura a pé (*Busted*).
- `Assets/Scripts/Police/PoliceVehicleAI.cs` — Controlador de IA das viaturas policiais com estados de patrulha (`Patrol`) e perseguição agressiva (`Pursuit`), giroflex vermelho/azul, sirene sonora com efeito Doppler/panning top-down e navegação híbrida pelo grafo viário + interceptação direta.
- `Assets/Scripts/Missions/VehicleContractData.cs` — `ScriptableObject` que define os dados de um contrato de roubo/entrega (veículo alvo, recompensas, bônus de tempo e geração de *heat*).
- `Assets/Scripts/Missions/MissionEvents.cs` — Enums (`MissionPhase`, `GameOverReason`, `HeatSourceType`) e structs imutáveis (`ActiveMissionInfo`, `MissionCompletionResult`, `GameOverResult`, `HeatEventPayload`) dos eventos de missão, polícia e fim de jogo.
- `Assets/Scripts/Missions/DeliveryZone.cs` — Trigger isolado de zona de desmanche que valida o veículo alvo, destaca a baia ativa no chão, calcula a pose segura de ejeção (`GetSafeEjectPose`) e dispara `OnVehicleDelivered`.
- `Assets/Scripts/Missions/MissionTargetIndicator.cs` — Seta direcional 3D que orbita o jogador/veículo atual e aponta para o carro alvo (verde) ou para a `DeliveryZone` (amarela).
- `Assets/Scripts/Missions/MissionManager.cs` — Orquestrador do loop infinito de contratos, gerenciando spawn de alvos, transição de fases, entrega, recompensas, banco de tempo e encerramento por tempo, prisão (*Busted*) ou explosão veicular (*VehicleDestroyed*).
- `Assets/Scripts/Traffic/TrafficWaypoint.cs` — Nó direcional de faixa de rolamento com conexões serializadas para os próximos waypoints, vínculo opcional com semáforo (`TrafficIntersection`) e visualização via Gizmos.
- `Assets/Scripts/Traffic/TrafficIntersection.cs` — Controlador de semáforo e reserva de cruzamento (ciclos Verde/Amarelo/Vermelho por eixo Horizontal e Vertical + atualização visual de lâmpadas emissivas).
- `Assets/Scripts/Traffic/TrafficRoadGraph.cs` — Grafo viário serializado que armazena todos os `TrafficWaypoint`s da Lapa e fornece pontos de spawn livres de obstáculos (`OverlapSphereNonAlloc`).
- `Assets/Scripts/Traffic/TrafficVehicleAI.cs` — Motorista autônomo de IA que navega pelo grafo de faixas, freia diante de trânsito/pedestres, executa manobra de desatolamento e cede o controle quando roubado pelo jogador.
- `Assets/Scripts/Traffic/TrafficManager.cs` — Gerenciador de densidade de tráfego que instancia e recicla veículos autônomos em um anel ao redor do jogador e fornece/protege veículos-alvo de missão.
- `Assets/Scripts/UI/VehicleHudUI.cs` — HUD desacoplado que escuta `OnVehicleEntered` / `OnVehicleExited` e `VehicleHealth.OnHealthChanged` para exibir o nome do veículo e sua barra de vida/integridade em tempo real no canto inferior direito.
- `Assets/Scripts/UI/MissionHudUI.cs` — HUD desacoplado do sistema de missões e polícia que exibe o objetivo atual, banco de tempo, dinheiro acumulado (`$`), total de entregas, painel de 6 estrelas de procurado (`WantedStarsPanel`), alertas policiais e tela de Game Over contextual.
- `Assets/Scripts/Camera/TopDownCameraFollow.cs` — Câmera top-down suave que alterna o alvo (`SetTarget`) entre o jogador e o veículo atual, aplicando afastamento/aproximação vertical dinâmico (`maxSpeedExtraHeight`, `zoomOutSmoothTime`, `zoomInSmoothTime`) proporcional à velocidade normalizada (`NormalizedSpeed`) do veículo.
- `Assets/Scripts/Editor/LapaMapGenerator.cs` — Gerador procedural/modular de Editor (`Tools/GTA2/Build Lapa Map`) que constrói o mapa da Lapa e o grafo de tráfego (`Traffic_Network`) na `SampleScene` usando os kits `Roads` e `City-kit`.
- `Assets/Scripts/Editor/SceneSetupUtility.cs` — Utilitário de Editor (`Tools/GTA2/Generate All Vehicle Data & Prefabs`, `Tools/GTA2/Setup Player Scene`, `Tools/GTA2/Spawn Test Vehicles`, `Tools/GTA2/Setup Vehicle HUD`, `Tools/GTA2/Setup Traffic System`, `Tools/GTA2/Setup Mission System`, `Tools/GTA2/Setup Police System` e `Tools/GTA2/Setup Audio System`) para configurar toda a cena e assets automaticamente.
- `Assets/Audio/` — Clips `.wav` gerados proceduralmente (`EngineLoop_Standard.wav`, `EngineLoop_Sports.wav`, `EngineLoop_Heavy.wav`, `PoliceSiren_Loop.wav`, `PoliceRadio_StarIncrease.wav`).
- `Assets/Data/Vehicles/` — 20 `ScriptableObject`s (`VehicleData_*.asset`) com atributos balanceados por categoria para todos os veículos do `Car kit` (exceto karts).
- `Assets/Data/Contracts/` — 20 `ScriptableObject`s (`Contract_*.asset`) de contratos de roubo/entrega para o loop arcade.
- `Assets/Prefabs/Vehicles/` — 20 Prefabs completos (`Vehicle_*.prefab`) configurados com `Rigidbody`, `BoxCollider`, `VehicleController`, `VehicleHealth`, `VehicleEngineAudio`, `TrafficVehicleAI`, `VehicleData`, pontos de saída e referências das 4 rodas.
- `Assets/Models/` — Kits 3D Low-Poly (`Characters`, `Car kit`, `City-kit`, `Roads`).
- `Assets/Materials/` — Materiais URP (`PlayerSkin.mat`, `Ground.mat`, `CarColormap.mat`, `RoadsColormap.mat`, `CityColormap.mat`, `MissionArrow.mat`, `DeliveryZoneHighlight.mat`).
- `Assets/Animations/` — `PlayerAnimator.controller`.

---

## Diretrizes de Código C# para a IA

- Use C# moderno: pattern matching, `var` quando o tipo for explícito, métodos com expressão de corpo (`=>`) para propriedades simples.
- Variáveis privadas serializáveis: `[SerializeField] private float speed;`.
- Métodos de ciclo de vida (`Awake`, `Start`, `Update`, `FixedUpdate`) devem ser privados ou protegidos.
- Sempre calcule física veicular dentro do `FixedUpdate()`.
- Inclua documentação XML (`/// <summary>`) em métodos públicos de eventos e controladores.
