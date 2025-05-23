import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import {
  BehaviorSubject,
  catchError,
  from,
  Observable,
  Subject,
  tap,
} from 'rxjs';

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
  private signalUrl = 'http://localhost:5005/hubs/chat';

  private readonly _hubConnection: HubConnection;

  constructor(private http: HttpClient) {
    this._hubConnection = new HubConnectionBuilder()
      .withUrl(this.signalUrl)
      .withAutomaticReconnect()
      .build();

    // Monitora mudanças no estado da conexão
    this._hubConnection.onclose(() => {
      console.log('Connection closed');
      this.connectionStatusSubject.next(false);
    });

    this._hubConnection.onreconnecting(() => {
      console.log('Connection reconnecting');
      this.connectionStatusSubject.next(false);
    });

    this._hubConnection.onreconnected(() => {
      console.log('Connection reconnected');
      this.connectionStatusSubject.next(true);
    });
  }

  get hubConnection(): HubConnection {
    return this._hubConnection;
  }

  public joinChat(userId: string): Observable<any> {
    this.userId = userId;
    return this.http.post(`${this.apiUrl}/chat/join`, { userId });
  }

  public connectChat(): Observable<void> {
    console.log(`Current connection state: ${this._hubConnection.state}`);

    if (this._hubConnection.state !== 'Disconnected') {
      console.log(
        `Connection is already in ${this._hubConnection.state} state`
      );

      // Se já está conectado, emitimos true para o status
      if (this._hubConnection.state === 'Connected') {
        this.connectionStatusSubject.next(true);
      }

      return new Observable<void>((observer) => {
        if (this._hubConnection.state === 'Connected') {
          observer.next();
          observer.complete();
        } else {
          // Estamos em um estado intermediário, esperamos a conexão completar
          const checkConnection = setInterval(() => {
            if (this._hubConnection.state === 'Connected') {
              clearInterval(checkConnection);
              this.connectionStatusSubject.next(true);
              observer.next();
              observer.complete();
            } else if (this._hubConnection.state === 'Disconnected') {
              clearInterval(checkConnection);
              this.connectionStatusSubject.next(false);
              observer.error(new Error('Connection failed'));
            }
          }, 500);
        }
      });
    }

    // Se está desconectado, iniciamos a conexão
    return from(this._hubConnection.start()).pipe(
      tap(() => {
        console.log('SignalR connection started successfully');
        this.connectionStatusSubject.next(true);
      }),
      catchError((error) => {
        console.error('Error starting SignalR connection:', error);
        this.connectionStatusSubject.next(false);
        throw error;
      })
    );
  }

  public sendMessage(messageText: string): void {
    if (this._hubConnection.state === 'Connected') {
      console.log(
        `Sending message: userId=${this.userId}, message=${messageText}`
      );

      // O SignalR vai injetar o CancellationToken automaticamente
      // Nós só precisamos fornecer os parâmetros userId e messageText
      this._hubConnection
        .invoke('SendMessage', this.userId, messageText)
        .then(() => {
          console.log('Message sent successfully');
        })
        .catch((err) => {
          console.error('Failed to send message:', err);

          // Informações de depuração
          console.log('Parameters used:', {
            userId: this.userId,
            messageText: messageText,
          });

          // Como alternativa, poderíamos tentar converter em um objeto
          console.log('Trying alternative approach...');
          try {
            // Em alguns casos, os hubs SignalR esperam os parâmetros em ordem específica
            // e são sensíveis a null/undefined
            if (!this.userId) {
              console.warn('userId is empty, using fallback');
            }

            this._hubConnection
              .invoke(
                'SendMessage',
                this.userId || 'anonymous-user',
                messageText || ''
              )
              .catch((e) => console.error('Alternative also failed:', e));
          } catch (alt_err) {
            console.error('Error in alternative approach:', alt_err);
          }
        });
    } else {
      console.warn('Cannot send message: not connected to hub');
    }
  }

  public getUserId(): string {
    return this.userId;
  }
}
