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
      console.log('Conexão fechada');
      this.connectionStatusSubject.next(false);
    });

    this._hubConnection.onreconnecting(() => {
      console.log('Reconectando...');
      this.connectionStatusSubject.next(false);
    });

    this._hubConnection.onreconnected(() => {
      console.log('Reconexão bem-sucedida');
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
    console.log(`Estado atual da conexão: ${this._hubConnection.state}`);

    if (this._hubConnection.state !== 'Disconnected') {
      console.log(`Conexão já está no estado ${this._hubConnection.state}`);

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
              observer.error(new Error('Falha na conexão'));
            }
          }, 500);
        }
      });
    }

    // Se está desconectado, iniciamos a conexão
    return from(this._hubConnection.start()).pipe(
      tap(() => {
        console.log('Conexão SignalR iniciada com sucesso');
        this.connectionStatusSubject.next(true);
      }),
      catchError((error) => {
        console.error('Erro ao iniciar conexão SignalR:', error);
        this.connectionStatusSubject.next(false);
        throw error;
      })
    );
  }

  public sendMessage(messageText: string): void {
    if (this._hubConnection.state === 'Connected') {
      console.log(
        `Enviando mensagem: usuário=${this.userId}, mensagem=${messageText}`
      );

      // O SignalR vai injetar o CancellationToken automaticamente
      // Nós só precisamos fornecer os parâmetros userId e messageText
      this._hubConnection
        .invoke('SendMessage', this.userId, messageText)
        .then(() => {
          console.log('Mensagem enviada com sucesso');
        })
        .catch((err) => {
          console.error('Falha ao enviar mensagem:', err);

          // Informações de depuração
          console.log('Parâmetros utilizados:', {
            userId: this.userId,
            messageText: messageText,
          });

          // Como alternativa, poderíamos tentar converter em um objeto
          console.log('Tentando abordagem alternativa...');
          try {
            // Em alguns casos, os hubs SignalR esperam os parâmetros em ordem específica
            // e são sensíveis a null/undefined
            if (!this.userId) {
              console.warn('userId está vazio, usando valor padrão');
            }

            this._hubConnection
              .invoke(
                'SendMessage',
                this.userId || 'usuário-anônimo',
                messageText || ''
              )
              .catch((e) => console.error('Alternativa também falhou:', e));
          } catch (alt_err) {
            console.error('Erro na abordagem alternativa:', alt_err);
          }
        });
    } else {
      console.warn('Não é possível enviar mensagem: não conectado ao hub');
    }
  }

  public getUserId(): string {
    return this.userId;
  }
}
