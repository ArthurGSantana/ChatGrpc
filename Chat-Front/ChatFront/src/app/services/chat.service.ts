import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, Subject } from 'rxjs';

export interface ChatMessage {
  sender: string;
  content: string;
  timestamp: Date;
}

@Injectable({
  providedIn: 'root',
})
export class ChatService {
  private socket: WebSocket | null = null;
  private userId = '';
  private messagesSubject = new Subject<ChatMessage>();
  private connectionStatusSubject = new BehaviorSubject<boolean>(false);

  public messages$ = this.messagesSubject.asObservable();
  public connectionStatus$ = this.connectionStatusSubject.asObservable();

  private apiUrl = 'http://localhost:5005/api';
  private wsUrl = 'ws://localhost:5005/ws';

  constructor(private http: HttpClient) {}

  public joinChat(userId: string): Observable<any> {
    this.userId = userId;
    return this.http.post(`${this.apiUrl}/chat/join`, { userId });
  }

  public connectWebSocket(): void {
    if (this.socket) {
      this.socket.close();
    }

    const wsUrlWithUserId = `${this.wsUrl}?userId=${encodeURIComponent(
      this.userId
    )}`;
    this.socket = new WebSocket(wsUrlWithUserId);

    this.socket.onopen = () => {
      console.log('WebSocket connection established');
      this.connectionStatusSubject.next(true);
    };

    this.socket.onmessage = (event) => {
      try {
        const message = JSON.parse(event.data);

        if (message.type === 'connection') {
          console.log('Connection confirmed:', message.message);
        } else {
          this.messagesSubject.next({
            sender: message.userId || 'System',
            content: message.message,
            timestamp: new Date(message.timestamp),
          });
        }
      } catch (error) {
        console.error('Error parsing message:', error);
      }
    };

    this.socket.onclose = () => {
      console.log('WebSocket connection closed');
      this.connectionStatusSubject.next(false);
    };

    this.socket.onerror = (error) => {
      console.error('WebSocket error:', error);
      this.connectionStatusSubject.next(false);
    };
  }

  public sendMessage(content: string): void {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      const message = {
        message: content,
      };
      this.socket.send(JSON.stringify(message));
    } else {
      console.error('WebSocket is not connected');
    }
  }

  public disconnect(): void {
    if (this.socket) {
      this.socket.close();
      this.socket = null;
    }
  }
  public getUserId(): string {
    return this.userId;
  }

  /**
   * Testa diferentes URLs de WebSocket para encontrar uma que funcione
   * Útil para diagnosticar problemas de conexão
   */
  public testWebSocketConnections(): void {
    const testUrls = [
      'ws://localhost:5005', // Raiz
      'ws://localhost:5005/ws', // Endpoint /ws
      'ws://localhost:5005/chat', // Endpoint /chat
      'ws://localhost:5005/socket', // Endpoint /socket
    ];

    console.log('Testando diferentes URLs de WebSocket...');

    testUrls.forEach((url) => {
      try {
        console.log(`Testando conexão em: ${url}`);
        const testSocket = new WebSocket(url);

        testSocket.onopen = () => {
          console.log(`✅ Conexão bem-sucedida em: ${url}`);
          testSocket.close();
        };

        testSocket.onerror = () => {
          console.log(`❌ Falha na conexão em: ${url}`);
        };

        setTimeout(() => {
          if (
            testSocket.readyState !== WebSocket.OPEN &&
            testSocket.readyState !== WebSocket.CLOSED
          ) {
            console.log(`⏱️ Tempo esgotado para: ${url}`);
            testSocket.close();
          }
        }, 5000);
      } catch (error) {
        console.error(`Erro ao testar ${url}:`, error);
      }
    });
  }
}
