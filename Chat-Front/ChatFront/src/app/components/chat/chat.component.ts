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
    this.subscriptions.push(
      this.chatService.messages$.subscribe((message) => {
        this.messages.push(message);
        setTimeout(() => {
          this.scrollToBottom();
        }, 0);
      })
    );

    this.subscriptions.push(
      this.chatService.connectionStatus$.subscribe((status) => {
        this.isConnected = status;

        if (!status && !this.reconnecting) {
          this.attemptReconnection();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach((subscription) => subscription.unsubscribe());
    this.chatService.disconnect();
  }
  sendMessage(): void {
    if (this.newMessage.trim() && this.isConnected) {
      this.chatService.sendMessage(this.newMessage.trim());
      this.newMessage = '';
    }
  }
  
  testConnections(): void {
    console.log('Iniciando diagnóstico de conexão...');
    this.chatService.testWebSocketConnections();
  }

  private attemptReconnection(): void {
    this.reconnecting = true;

    const reconnectSubscription = interval(3000)
      .pipe(takeWhile(() => !this.isConnected, true))
      .subscribe({
        next: () => {
          if (!this.isConnected) {
            console.log('Tentando reconectar ao servidor...');
            this.chatService.connectWebSocket();
          } else {
            this.reconnecting = false;
            reconnectSubscription.unsubscribe();
          }
        },
        complete: () => {
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
