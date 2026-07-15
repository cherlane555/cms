import { Component, Input, OnChanges, signal } from '@angular/core';
import { toDataURL } from 'qrcode';
import { ButtonModule } from 'primeng/button';

/**
 * Downloadable QR code linking to the public course page:
 * https://www.uuu.com.tw/Course/Show/{pkid}/{CourseId}. Title shows the CourseId.
 */
@Component({
  selector: 'app-course-qr',
  imports: [ButtonModule],
  templateUrl: './course-qr.html',
  styleUrl: './course-qr.scss',
})
export class CourseQr implements OnChanges {
  @Input({ required: true }) pkid = 0;
  @Input({ required: true }) courseId = '';

  protected readonly dataUrl = signal('');

  get url(): string {
    return `https://www.uuu.com.tw/Course/Show/${this.pkid}/${encodeURIComponent(this.courseId)}`;
  }

  ngOnChanges(): void {
    if (!this.pkid || !this.courseId) {
      this.dataUrl.set('');
      return;
    }
    toDataURL(this.url, { width: 200, margin: 1 }).then((d) => this.dataUrl.set(d));
  }

  download(): void {
    const href = this.dataUrl();
    if (!href) {
      return;
    }
    const anchor = document.createElement('a');
    anchor.href = href;
    anchor.download = `${this.courseId}.png`;
    anchor.click();
  }
}
