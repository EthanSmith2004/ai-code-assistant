import { Cart, CartItem } from '../models/cart';

const carts = new Map<string, Cart>();

export const cartService = {
  find(id: string): Cart {
    return carts.get(id) ?? { id, items: [] };
  },
  addItem(id: string, item: CartItem): Cart {
    const cart = cartService.find(id);
    cart.items.push(item);
    carts.set(id, cart);
    return cart;
  },
  clear(id: string) {
    carts.delete(id);
  }
};
