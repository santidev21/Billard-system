import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService } from '../../core/api.service';
import { Product } from '../../core/models';
import { fmtMoney } from '../../core/format';
import { SpinnerComponent } from '../../shared/spinner.component';

@Component({
  selector: 'app-catalog',
  imports: [FormsModule, SpinnerComponent],
  templateUrl: './catalog.component.html',
  styleUrls: ['./catalog.component.css'],
  standalone: true,
})
export class CatalogComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly fmtMoney = fmtMoney;

  readonly products = signal<Product[]>([]);
  readonly loading = signal(false);
  newProductName = '';
  newProductPrice = 0;

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  private async reload(): Promise<void> {
    this.loading.set(true);
    try {
      this.products.set(await this.api.getProducts());
    } finally {
      this.loading.set(false);
    }
  }

  async addProduct(): Promise<void> {
    if (!this.newProductName.trim() || this.newProductPrice <= 0) {
      return;
    }
    this.loading.set(true);
    try {
      await this.api.createProduct(this.newProductName.trim(), this.newProductPrice);
      this.newProductName = '';
      this.newProductPrice = 0;
      await this.reload();
    } finally {
      this.loading.set(false);
    }
  }

  async removeProduct(id: string): Promise<void> {
    this.loading.set(true);
    try {
      await this.api.deactivateProduct(id);
      await this.reload();
    } finally {
      this.loading.set(false);
    }
  }
}