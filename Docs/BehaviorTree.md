# Behavior Tree 프레임워크

2026-11-03 초기 구현. 네임스페이스는 `UnityFramework.BehaviorTree`다. 기존 캐릭터·FSM·전투 프리팹에는 적용하지 않았다.

## 구성과 접근 범위

- `Runtime/BehaviorTree/Framework.BehaviorTree.asmdef`: 게임 타입이나 FSM에 의존하지 않는 실행 기반.
- `Editor/BehaviorTree/Framework.BehaviorTree.Editor.asmdef`: GraphView 편집·실행 관찰과 블랙보드 편집.
- `Editor/BehaviorTree/Tests/Framework.BehaviorTree.Tests.asmdef`: Editor 전용 NUnit 테스트.
- 에셋(`BlackboardData`, `BehaviorTreeData`), 실행 제어(`BehaviorTreeController`, 선택적 `BehaviorTreeComponent`), 확장 기반(`TaskNode`, `TaskNodeData<TTask>`, `Decorator`, `Service`)과 필요한 계약은 public이다.
- 블랙보드 구현체, 키 정의와 값 슬롯, 실행 Model·노드 메모리, 기본 분기·Task·Service 구현은 internal이다. 블랙보드의 조회·쓰기와 컴포넌트의 owner 연결 계약은 명시적으로 인터페이스를 구현한다.
- 런타임 내부 접근은 지정된 BT 에디터 및 테스트 어셈블리에만 `InternalsVisibleTo`로 허용한다. 관찰을 위해 게임 런타임에 리플렉션이나 등록 스캔을 추가하지 않았다.

모든 경로의 기준은 `UnitySisters/Assets/Plugins/Framework/`다.

## 에디터에서 만들기

1. `UnityFramework/Behavior Tree/Editor` 메뉴를 연다.
2. `New Tree`, `New Blackboard`로 에셋을 만든다. Tree에 블랙보드 스키마를 지정한다. Project의 Create 메뉴로도 생성할 수 있다.
3. 왼쪽에서 키 이름·타입·기본값을 정의한다. 지원 타입은 bool, int, float, Vector3, UnityEngine.Object다. Object 키의 코드상 타입도 `UnityEngine.Object`를 사용한다.
4. `Add node`나 그래프 컨텍스트 메뉴로 Selector, Sequence, Task를 생성한다. Root를 우클릭해 `Set root / select node`를 선택하고 시작 노드를 클릭한다. Composite를 우클릭해 `Connect child / select node`를 선택한 뒤 자식 노드를 클릭한다. Esc·우클릭·빈 공간 클릭으로 연결을 취소할 수 있다. Task는 자식 연결을 시작할 수 없다.
5. 노드를 선택해서 설정·Decorator를 편집하고, Composite에는 Service를 부착한다. 기본 Task는 Wait와 SetBlackboardValue, 기본 Service는 SetBlackboardValueService, 기본 조건은 BlackboardDecorator다.
6. 오른쪽의 자식 Up/Down으로 실행 우선순위를 정한다. 화면의 좌우 위치가 우선순위를 결정하지 않으며 노드에 탐색 순서 번호를 표시한다.
7. `Validate / Save` 또는 Ctrl+S로 검사 후 저장한다. 누락된 키·잘못된 비교·끊긴 노드·순환·중복 부모·빈 Composite·플레이어 빌드에 없는 확장 타입 등을 검사한다.

노드 이동·추가·삭제·연결·설정 편집은 Undo/Redo를 사용한다. 키 참조와 노드 연결은 이름이나 목록 인덱스가 아닌 저장된 ID로 연결한다. 키 이름 변경과 노드 위치 변경은 해당 ID를 바꾸지 않는다. 키 타입을 변경하거나 키를 삭제하면 기존 사용처를 재검토해야 한다.

연결 표시는 기존 FSM 에디터를 참고해 포트를 숨기고, 박스 테두리 사이의 직선과 방향 화살표로 표현한다. 연결 중에는 원본 노드를 표시하고 미리보기 선을 제공한다. 겹치는 박스 사이에는 방향을 오인할 화살표를 표시하지 않는다. 이 표현을 위해 BT의 실행 순서나 부모·자식 규칙을 바꾸지는 않는다.

## 실행하기

간단히 실행하려면 별도 오브젝트에 `BehaviorTreeComponent`를 붙이고 Tree를 지정한다. 이 컴포넌트는 OnEnable에서 Start, Update에서 Tick, OnDisable에서 Stop한다. owner가 지정된 경우 그 기능을 사용하며, 없으면 자신의 오브젝트에서 필요한 컴포넌트 인터페이스를 찾는다. 해당 기능 조회는 `IBehaviorTreeOwner` 계약으로만 노출한다.

게임의 처리 주기와 조립 구조를 따로 제어할 때는 `BehaviorTreeData.CreateController(owner)`로 실행기를 만들고 `Start()`, `Tick(deltaTime)`, `Stop()`을 호출한다. 외부 AIController가 이를 소유할 수 있으며, 기존 캐릭터를 변경할 필요는 없다.

```csharp
private BehaviorTreeController treeController;
private BlackboardKeyReference<bool> hasTarget;

private void Initialize(BehaviorTreeData data, IBehaviorTreeOwner owner)
{
    treeController = data.CreateController(owner);
    hasTarget = data.BlackboardData.GetKey<bool>("HasTarget");
    treeController.Start();
}

private void UpdateTarget(bool exists)
{
    treeController.BlackboardWriter.SetValue(hasTarget, exists);
}
```

`GetKey<T>`는 키 이름으로 참조를 구하는 조립 시점의 API다. 매 Tick마다 이름으로 다시 찾지 말고 참조를 보관한다. TaskNodeData·Decorator·Service의 직렬화 필드에는 `BlackboardKeyReference<T> key = new();`를 둘 수 있으며 BT 에셋 안에서 타입에 맞는 키를 선택한다. 런타임 값 조회는 `IReadOnlyBlackboard`, 수정은 `IBlackboardWriter`로 제공한다. 키 없음·타입 불일치는 잘못된 값을 묵인하지 않고 오류로 처리하며, 조회 실패를 분기하려면 `TryGetValue`를 사용한다.

## 실행과 수명

- 반환 상태는 Success, Failure, Running이다. Root가 끝나면 같은 Tick 안에서 무한 반복하지 않고 다음 Tick에 다시 시작한다. 정지는 Stop으로 명시한다.
- Task의 OnEnter는 진입 시 한 번 호출한다. Running 상태에서는 OnTick으로 진행을 확인하고, 완료·실패·중단·정지 시 OnExit를 호출한다. 행동 요청을 매 Tick마다 반복해서 보내지 않는다.
- Task 행동 객체는 타입별로 전역 공유하며, 직렬화된 설정은 노드별 TaskNodeData에 둔다. 개별 실행 상태는 `CreateMemory()`로 생성하고 OnEnter에서 초기화한다. 같은 에셋의 여러 실행기 사이에 타이머·대상·요청 핸들을 공유하지 않는다.
- Task와 Service의 OnExit에서 자신이 소유한 구독과 요청을 정리한다. 실행 중 예외가 나면 트리를 정지하고 활성 가지의 정리를 시도한 뒤 원래 오류를 전달한다.
- Service는 부착한 Composite가 활성화된 동안만 실행한다. OnEnter/OnExit와 설정한 양수 interval을 사용하며, 밀린 주기를 한 Tick 안에 여러 번 실행하지 않는다. 즉시 초기 정보가 필요하면 사용자 Service의 OnEnter에서 기록한다.
- 블랙보드는 정보를 저장한다. 인식·거리 계산·캐릭터 실행 상태의 원본을 대신하지 않는다. 필요한 정보를 외부 기능이나 Service가 갱신한다.

Task는 행동만 가진 TaskNode 파생 클래스와 설정을 가진 [Serializable] TaskNodeData<TTask> 파생 클래스로 나눈다. 행동에는 public 매개변수 없는 생성자를 제공하며, 에디터는 데이터 타입을 생성·편집한다. Decorator, Service 확장은 해당 기반 클래스를 상속한 `[Serializable]` 클래스와 매개변수 없는 생성자로 만든다. 런타임 어셈블리에 배치해야 한다. 에디터 전용 확장 타입은 플레이어용 트리에 저장할 수 없다. 데이터에서 직접 사용하는 키가 있다면 `Validate(BlackboardData, ICollection<string>)`에서 `schema.ContainsKey(key)` 등을 검사한다. Decorator의 Evaluate는 읽기·판단 역할로 작성한다.

## 조건 재평가와 중단

블랙보드 값이 실제로 바뀌면 재평가를 예약한다. 값 설정 도중 동기적으로 트리를 재진입하지 않으며 다음 Tick에서 처리한다. 블랙보드 이외의 조건을 사용하는 외부 기능은 `RequestEvaluation()`으로 재평가를 요청할 수 있다. 변경이 없을 때는 현재 실행 가지와 활성 Service만 진행한다.

- None: 실행 중 조건 변화로 중단하지 않는다.
- Self: 활성 노드의 조건이 거짓이 되면 해당 가지를 중단한다.
- LowerPriority: 이전에 탐색한 우선순위 가지의 조건이 거짓에서 참으로 바뀌면 낮은 우선순위 가지를 중단하고 다시 선택한다. Sequence 내부에서 탐색이 막혔던 조건도 확인한다.
- Both: 두 동작을 사용한다.

초기 구현은 블랙보드 변경을 공통 재평가 신호로 사용하고, 가지의 결합 조건 결과와 이전 탐색 경로를 기준으로 판단한다. 언리얼의 키별 옵저버 등록, 모든 Observer Notify 옵션, 복잡한 동시 실행의 완전한 복제는 아니다. Parallel·Subtree·네트워크 실행은 구현 범위에 포함하지 않았다.

BT 중단은 곧 캐릭터 행동 강제 취소를 의미하지 않는다. 실제 이동·공격 Task는 owner의 필요한 기능 인터페이스를 사용하고, Character·FSM이 승인·거절·완료·취소 가능 여부를 결정하도록 연결한다. 현재 Character.ExecuteCommand의 void 반환만으로 요청 승인을 판단하면 안 된다.

## 간단 추격과 컨트롤러 교환

`SampleScene`의 `SD_Unity_Chan`은 PlayerController, `SD_Unity_Chan (1)`은 AIController가 조작한다. 시작 배치는 추격을 볼 수 있도록 5m 간격을 둔다. Tab을 누르면 GameManager가 두 연결을 해제한 뒤 조작 대상을 교환하고 카메라와 추격 대상도 갱신한다. 키는 GameManager의 Swap Key에서 변경한다.

`Assets/Data/AI/ChasePlayer.asset`은 ChaseTargetTask 하나를 실행한다. Target Object 블랙보드 키에는 현재 플레이어 캐릭터의 Transform을 연결한다. Task는 IAICommandControl로 수평 이동 의도만 요청하며, AIController는 FixedUpdate에서 CharacterCommand를 Character.ExecuteCommand로 전달한다. BT 판단은 기존 BehaviorTreeComponent의 Update에서 수행한다. 캐릭터의 기존 이동 속도·중력·애니메이션·이동 잠금은 그대로 사용한다. Task의 Stopping Distance 기본값은 1.5m다.

이는 센서 없이 명시적으로 지정한 대상을 직선 추격하는 최소 연결이다. 장애물 우회, 감지·시야·타깃 선정, AI 공격은 포함하지 않는다. BT 노드 정의에는 캐릭터별 가변 상태를 저장하지 않고 조작 대상과 명령은 AIController 인스턴스에 둔다. AIController 오브젝트를 선택하면 기존 BT 창에서 실행을 관찰할 수 있다.

## 플레이 관찰

### Task 공유 구조

TaskManager는 타입별 TaskNode 행동 객체를 하나씩 생성해 보관하는 internal 매니저다. TaskNodeData<TTask>가 매니저에서 행동을 가져오고, BehaviorTreeModel은 초기화 시 노드별 실행 메모리와 공유 행동 참조를 보관한다. Tick에는 매니저 조회나 리플렉션을 추가하지 않았다.

게임 측 Task 데이터는 TaskNodeData<TTask>로 확장한다. 비제네릭 TaskNodeData의 직접 상속과 행동 제공 재정의는 프레임워크 내부로 제한했으며, null 행동이 반환되면 실행기 생성 시 예외로 거절한다. 행동 없는 Task를 빈 분기로 실행해 Success로 처리하지 않는다.

- TaskNode: 설정이나 owner를 보관하지 않는 공유 행동. Enter/Tick/Exit에 context, 해당 노드의 TaskNodeData, 개별 memory를 전달받는다.
- TaskNodeData<TTask>: 블랙보드 키·정지 거리·대기 시간 등 노드별 직렬화 설정과 설정 검증. 에디터의 Task 선택 대상이다.
- BehaviorNodeMemory: 캐릭터·노드별 진행 상태와 CreateMemory 반환값. 같은 Task 타입을 여러 노드·트리에서 사용해도 메모리는 공유하지 않는다.

WaitTask/WaitTaskData, SetBlackboardValueTask/SetBlackboardValueTaskData, ChaseTargetTask/ChaseTargetTaskData를 분리했다. 현재 프로젝트에서 직렬화된 Task 에셋은 ChasePlayer였고, 타입 참조를 ChaseTargetTaskData로 이전하면서 대상 키와 정지 거리를 유지했다. 프로젝트 밖에서 이전 Task API로 작성한 확장이나 에셋은 새 데이터/행동 계약으로 별도 이전해야 한다. Decorator와 Service는 이번 분리 대상이 아니다.

플레이 중 `BehaviorTreeComponent`가 붙은 오브젝트를 선택하면 실제 실행기의 에셋·블랙보드 값과 노드 상태를 표시한다. 실행 중은 주황색, 성공은 초록색, 실패는 빨간색, 중단·정지는 보라색이다. 비활성 노드의 색은 마지막 결과이며 현재 실행 중이라는 뜻이 아니다.

플레이에서는 에셋 선택·추가·연결·상세 설정·저장을 막고, 조회·이동·확대만 허용한다. 플레이 종료 시 이전 편집 에셋으로 돌아온다. 표시 갱신은 에디터에서 수행하며 런타임 내부 상태는 지정한 에디터 어셈블리가 직접 읽는다.

## 확인한 범위

- Unity 컴파일 오류 없음.
- BT 전용 EditMode 테스트 42개 통과: null Task의 실행기 생성 거절·공유 Task 동일성·노드/실행기별 메모리 격리·서로 다른 설정의 실행, 타입 계약, 변경 알림, 개별 블랙보드·메모리, 키 이름 변경, Selector/Sequence 순서, Running 재개, 종료·재시작, 예외 정리, Service 수명, Self/LowerPriority/Both/None, 중첩 우선순위, 잘못된 연결·키, 실제 에셋 저장·재로드, 에디터 Undo/Redo와 관찰 편집 차단 등. 연결 표현 변경에서는 수평·수직·대각선·역방향 테두리 좌표, 겹침, 노드 선택 연결·거절·취소·관찰 전환·연결 Undo/Redo를 추가 검증했다.
- 안정적으로 실행 중인 Wait의 반복 Tick에서 해당 테스트 구간의 관리 힙 할당 0을 확인했다. 모든 Task·조건·Service의 무할당을 보장한다는 뜻은 아니다.
- FSM형 연결 표시 변경은 컴퓨터 컨트롤 없이 소스·Unity CLI로 작업했다. 실제 Editor 패널에서 USS의 포트 숨김, 네이티브 선 제어점의 직선 정렬, 노드 이동 후 화살표 갱신, 확대 후 선과 화살표의 중간점 일치를 확인했다. 최종 화면의 가독성과 실제 마우스 조작감 검수는 별도로 남아 있다.
- 임시로 생성한 실제 Tree/Blackboard와 컴포넌트를 Play mode에서 실행해 프레임 진행, Service 값 갱신, 낮은 우선순위 가지의 Aborted 전환, 창의 실행 경로·값 표시, 수정 차단, 컴포넌트 정지·재시작, 편집 모드 복귀를 확인했다. 검증용 에셋·오브젝트는 정리했다.
- 실제 SampleScene에서 프레임 진행, 추격, Unity 가상 키보드의 Tab 양방향 교환, 교환 전후 플레이어 이동 입력, 카메라·블랙보드 대상 갱신, 반복 교환, AI의 기존 이동 잠금 준수를 확인했다. OS 키보드·마우스 조작은 사용하지 않았다. 물리 키보드 조작감, AI 공격·센서·경로 탐색, 다수 캐릭터 부하 측정, 전체 플레이어 빌드는 미검증이다.
