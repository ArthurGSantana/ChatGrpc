import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-name-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './name-dialog.component.html',
  styleUrl: './name-dialog.component.scss',
})
export class NameDialogComponent {
  @Output() nameSubmitted = new EventEmitter<string>();

  username: string = '';
  showError: boolean = false;

  onSubmit(): void {
    if (this.username.trim()) {
      this.showError = false;
      this.nameSubmitted.emit(this.username.trim());
    } else {
      this.showError = true;
    }
  }
}
