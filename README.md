# A Aventura de Pip: Vale dos Slimes

Jogo de plataforma 2D feito na Unity como trabalho individual da disciplina de **Desenvolvimento de Jogos Digitais** (Ciência da Computação – UNISAGRADO).

Autor: **Heitor Lupino**

## Sobre o jogo

Você controla o Pip e precisa atravessar o Vale dos Slimes até a bandeira final, pulando entre plataformas, desviando de espinhos, derrotando slimes (pulando em cima deles) e coletando moedas e corações pelo caminho. A fase tem checkpoints, então ao perder uma vida você volta para o último checkpoint ativado.

## Controles

| Ação | Tecla |
|------|-------|
| Andar | A / D ou setas |
| Pular | Espaço, W ou seta para cima |
| Começar / confirmar | Enter |
| Pausar | Esc ou P |
| Reiniciar | R |
| Voltar ao menu | M |
| Trocar música | N |

Também funciona com controle (gamepad).

## Como abrir o projeto

1. Instale a **Unity 6.6** (versão `6000.6.9f1`) pelo Unity Hub.
2. Clone o repositório:
   ```
   git clone https://github.com/heitorlupino/a-aventura-de-pip.git
   ```
3. No Unity Hub, clique em **Add > Add project from disk** e selecione a pasta clonada.
4. Abra a cena `Assets/Jogo/Cenas/Fase1.unity` e aperte **Play**.

## Estrutura

```
Assets/Jogo/
├── Cenas/      -> cena da fase (Fase1)
├── Scripts/    -> scripts C# (jogador, inimigos, câmera, UI, música...)
├── Sprites/    -> sprites do personagem, inimigos, cenário e interface
├── Fontes/     -> fontes pixel art (Press Start 2P e VT323, licença OFL)
└── Editor/     -> script de editor usado para montar a fase
```

## Principais scripts

- `PlayerController.cs` – movimento, pulo (com coyote time e jump buffer), dano e animação do jogador
- `Enemy.cs` – patrulha dos slimes e morte ao serem pisados
- `GameManager.cs` – estados do jogo (menu, jogando, pausa, vitória, game over), vidas e pontuação
- `UIManager.cs` – HUD com corações e moedas
- `CameraFollow.cs` / `Parallax.cs` – câmera seguindo o jogador e fundo com efeito parallax
- `Checkpoint.cs`, `Coin.cs`, `HeartPickup.cs`, `Hazard.cs`, `Goal.cs` – objetos da fase
- `MusicPlayer.cs` / `SfxLibrary.cs` – música e efeitos sonoros
