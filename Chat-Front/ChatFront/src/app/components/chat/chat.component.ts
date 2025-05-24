import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, interval } from 'rxjs';
import { takeWhile } from 'rxjs/operators';
import { ChatMessage, ChatService } from '../../services/chat.service';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss',
})
export class ChatComponent implements OnInit, OnDestroy {
  messages: ChatMessage[] = [];
  newMessage: string = '';
  isConnected: boolean = false;
  username: string = '';
  private reconnecting: boolean = false;

  private subscriptions: Subscription[] = [];

  constructor(private chatService: ChatService) {
    this.username = this.chatService.getUserId();
  }

  ngOnInit(): void {
    // Configura o handler de mensagem independentemente do estado da conexão
    this.setupMessageHandler();

    // Verifica o estado atual
    const currentState = this.chatService.hubConnection.state;
    console.log(`Current connection state: ${currentState}`);

    if (currentState === 'Connected') {
      this.isConnected = true;
    } else if (currentState === 'Connecting') {
      console.log('Connection is in progress, waiting for it to complete...');
    } else if (
      currentState === 'Disconnected' ||
      currentState === 'Disconnecting'
    ) {
      console.log('Connection is not established yet');
    }

    // Sempre inscreve-se no status da conexão para ouvir mudanças de estado
    this.subscriptions.push(
      this.chatService.connectionStatus$.subscribe((connected) => {
        console.log(
          `Connection status changed to: ${
            connected ? 'connected' : 'disconnected'
          }`
        );
        this.isConnected = connected;

        // Se acabou de desconectar, tenta reconectar automaticamente
        if (!connected && !this.reconnecting) {
          this.attemptReconnection();
        }
      })
    );
  }

  private setupMessageHandler(): void {
    this.chatService.hubConnection.on(
      'ReceiveMessage',
      (userId: string, message: string) => {
        const chatMessage: ChatMessage = {
          sender: userId,
          content: message,
          timestamp: new Date(),
        };
        this.messages.push(chatMessage);
        this.scrollToBottom();
      }
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach((subscription) => subscription.unsubscribe());
  }

  sendMessage(): void {
    if (this.newMessage.trim() && this.isConnected) {
      this.chatService.sendMessage(this.newMessage.trim());
      this.newMessage = '';
    }
  }

  private attemptReconnection(): void {
    if (this.reconnecting) {
      console.log('Already attempting to reconnect...');
      return;
    }

    console.log('Starting reconnection attempts...');
    this.reconnecting = true;

    const reconnectSubscription = interval(3000)
      .pipe(takeWhile(() => !this.isConnected && this.reconnecting, true))
      .subscribe({
        next: () => {
          if (!this.isConnected) {
            console.log('Attempting to reconnect...');
            this.chatService.connectChat().subscribe({
              next: () => {
                console.log('Reconnection successful!');
                this.reconnecting = false;
              },
              error: (err) => {
                console.error('Reconnection attempt failed:', err);
              },
            });
          } else {
            console.log('Connection restored!');
            this.reconnecting = false;
            reconnectSubscription.unsubscribe();
          }
        },
        complete: () => {
          console.log('Reconnection attempts completed');
          this.reconnecting = false;
        },
      });

    this.subscriptions.push(reconnectSubscription);
  }

  private scrollToBottom(): void {
    const chatContainer = document.querySelector('.chat-messages');
    if (chatContainer) {
      chatContainer.scrollTop = chatContainer.scrollHeight;
    }
  }
}
