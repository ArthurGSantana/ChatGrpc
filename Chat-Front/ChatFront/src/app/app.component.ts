import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { ChatComponent } from './components/chat/chat.component';
import { NameDialogComponent } from './components/name-dialog/name-dialog.component';
import { ChatService } from './services/chat.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, NameDialogComponent, ChatComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  providers: [ChatService],
})
export class AppComponent implements OnInit {
  showNameDialog = true;
  joinChatError = false;

  constructor(private chatService: ChatService) {}

  ngOnInit(): void {}

  onNameSubmitted(username: string): void {
    this.chatService.joinChat(username).subscribe({
      next: () => {
        this.joinChatError = false;
        this.chatService.connectChat().subscribe({
          next: () => {
            console.log('Successfully connected to chat!');
            this.showNameDialog = false;
          },
          error: (err) => {
            console.error('Error connecting to chat:', err);
            this.joinChatError = true;
          },
        });
      },
      error: (error) => {
        console.error('Error joining chat:', error);
        this.joinChatError = true;
      },
    });
  }
}
