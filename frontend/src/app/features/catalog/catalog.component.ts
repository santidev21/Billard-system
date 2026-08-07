import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ApiService } from '../../core/api.service';
import { ProductCategory } from '../../core/models';

@Component({
  selector: 'app-catalog',
  imports: [FormsModule],
  templateUrl: './catalog.component.html',
  styleUrls: ['./catalog.component.css'],
  standalone: true,
})
export class CatalogComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly categories = signal<ProductCategory[]>([]);
  newCategory = '';
  newProductName = '';
  newProductPrice = 0;
  selectedCategoryId = '';

  async ngOnInit(): Promise<void> {
    await this.reload();
  }

  private async reload(): Promise<void> {
    this.categories.set(await this.api.getProducts());
  }

  async addProduct(): Promise<void> {
    if (!this.selectedCategoryId || !this.newProductName.trim()) {
      return;
    }
    await this.api.createProduct(this.selectedCategoryId, this.newProductName.trim(), this.newProductPrice);
    this.newProductName = '';
    this.newProductPrice = 0;
    await this.reload();
  }
}